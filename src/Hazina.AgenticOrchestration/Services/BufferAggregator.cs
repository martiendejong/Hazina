using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.AgenticOrchestration.Services;

/// <summary>
/// Buffer aggregation service - reduces WebSocket message flood
/// Pattern: VibeTunnel buffer protocol
/// </summary>
public interface IBufferAggregator
{
    void AppendOutput(string sessionId, byte[] data);
    event EventHandler<BufferFlushedEventArgs>? BufferFlushed;
}

public class BufferFlushedEventArgs : EventArgs
{
    public string SessionId { get; set; } = string.Empty;
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public int MessageCount { get; set; }
}

public class BufferAggregator : IBufferAggregator, IDisposable
{
    private const int AggregationWindowMs = 75; // 75ms sweet spot
    private const byte MagicByte = 0xBF; // Buffer aggregation marker

    private readonly ConcurrentDictionary<string, BufferState> _buffers = new();
    private readonly Timer _flushTimer;

    public event EventHandler<BufferFlushedEventArgs>? BufferFlushed;

    public BufferAggregator()
    {
        _flushTimer = new Timer(
            callback: _ => FlushBuffersAsync().GetAwaiter().GetResult(),
            state: null,
            dueTime: AggregationWindowMs,
            period: AggregationWindowMs
        );
    }

    public void AppendOutput(string sessionId, byte[] data)
    {
        var buffer = _buffers.GetOrAdd(sessionId, _ => new BufferState());

        lock (buffer.Lock)
        {
            buffer.Append(data);
            buffer.LastUpdate = DateTime.UtcNow;
        }
    }

    private async Task FlushBuffersAsync()
    {
        var now = DateTime.UtcNow;

        foreach (var (sessionId, buffer) in _buffers)
        {
            bool shouldFlush;
            byte[]? data = null;
            int messageCount = 0;

            lock (buffer.Lock)
            {
                var timeSinceUpdate = (now - buffer.LastUpdate).TotalMilliseconds;
                shouldFlush = timeSinceUpdate >= AggregationWindowMs && buffer.HasData;

                if (shouldFlush)
                {
                    data = buffer.GetData();
                    messageCount = buffer.MessageCount;
                    buffer.Clear();
                }
            }

            if (shouldFlush && data != null)
            {
                var aggregatedData = CreateAggregatedMessage(sessionId, data);

                BufferFlushed?.Invoke(this, new BufferFlushedEventArgs
                {
                    SessionId = sessionId,
                    Data = aggregatedData,
                    MessageCount = messageCount
                });
            }
        }

        await Task.CompletedTask;
    }

    private static byte[] CreateAggregatedMessage(string sessionId, byte[] data)
    {
        // Message format:
        // [0] Magic byte (0xBF)
        // [1-4] Length (int32, little-endian)
        // [5-20] Session ID (16-byte GUID)
        // [21+] Data

        var sessionGuid = Guid.Parse(sessionId);
        var guidBytes = sessionGuid.ToByteArray();

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(MagicByte);
        writer.Write(data.Length); // int32
        writer.Write(guidBytes); // 16 bytes
        writer.Write(data);

        return ms.ToArray();
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();
    }
}

internal class BufferState
{
    public object Lock { get; } = new();
    private readonly List<byte[]> _chunks = new();
    public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    public int MessageCount { get; private set; }

    public bool HasData => _chunks.Count > 0;

    public void Append(byte[] data)
    {
        _chunks.Add(data);
        MessageCount++;
    }

    public byte[] GetData()
    {
        if (_chunks.Count == 0)
        {
            return Array.Empty<byte>();
        }

        if (_chunks.Count == 1)
        {
            return _chunks[0];
        }

        // Combine all chunks
        var totalLength = _chunks.Sum(c => c.Length);
        var result = new byte[totalLength];
        var offset = 0;

        foreach (var chunk in _chunks)
        {
            Buffer.BlockCopy(chunk, 0, result, offset, chunk.Length);
            offset += chunk.Length;
        }

        return result;
    }

    public void Clear()
    {
        _chunks.Clear();
        MessageCount = 0;
    }
}
