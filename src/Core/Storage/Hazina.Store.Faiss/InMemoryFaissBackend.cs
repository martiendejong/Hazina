using System.Buffers.Binary;
using System.Collections.Concurrent;

namespace Hazina.Store.Faiss;

/// <summary>
/// Default in-memory FAISS backend that performs exact cosine similarity search.
/// Equivalent to a FAISS <c>IndexFlatIP</c> with L2-normalized vectors.
/// Thread-safe.
/// </summary>
/// <remarks>
/// All vectors are stored in memory. This is suitable for datasets up to ~50K
/// embeddings. For larger datasets use a native FAISS backend via
/// <see cref="IFaissIndexBackend"/>.
/// </remarks>
public sealed class InMemoryFaissBackend : IFaissIndexBackend
{
    private readonly ConcurrentDictionary<string, float[]> _index = new();

    /// <inheritdoc/>
    public void Upsert(string key, float[] vector)
    {
        // Normalize before storing so the dot product equals cosine similarity
        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm > 0f)
        {
            var normalized = vector.Select(v => v / norm).ToArray();
            _index[key] = normalized;
        }
        else
        {
            _index[key] = vector;
        }
    }

    /// <inheritdoc/>
    public bool Remove(string key) => _index.TryRemove(key, out _);

    /// <inheritdoc/>
    public IEnumerable<(string key, float similarity)> Search(float[] query, int topK)
    {
        // L2-normalize query
        var norm = MathF.Sqrt(query.Sum(v => v * v));
        var normalizedQuery = norm > 0f ? query.Select(v => v / norm).ToArray() : query;

        // Brute-force inner product (= cosine similarity for normalized vectors)
        return _index
            .AsParallel()
            .Select(kv =>
            {
                var dot = 0f;
                var vec = kv.Value;
                for (var i = 0; i < Math.Min(normalizedQuery.Length, vec.Length); i++)
                    dot += normalizedQuery[i] * vec[i];
                return (key: kv.Key, similarity: dot);
            })
            .OrderByDescending(r => r.similarity)
            .Take(topK);
    }

    /// <summary>
    /// Tries to retrieve the stored (normalized) vector for the given key.
    /// </summary>
    /// <param name="key">Embedding key.</param>
    /// <param name="vector">The stored float vector, or <c>null</c> if not found.</param>
    /// <returns><c>true</c> if found.</returns>
    public bool TryGetVector(string key, out float[]? vector) =>
        _index.TryGetValue(key, out vector);

    /// <summary>
    /// Returns the number of vectors currently indexed.
    /// </summary>
    public int Count => _index.Count;

    /// <inheritdoc/>
    /// <remarks>
    /// Serializes each entry as: [key-length (int32 LE)] [key bytes (UTF-8)]
    /// [vector-length (int32 LE)] [float[] (IEEE 754 little-endian)].
    /// </remarks>
    public void Save(string path)
    {
        using var fs = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(fs);

        foreach (var (key, vector) in _index)
        {
            var keyBytes = System.Text.Encoding.UTF8.GetBytes(key);
            writer.Write(keyBytes.Length);
            writer.Write(keyBytes);
            writer.Write(vector.Length);
            foreach (var f in vector)
                writer.Write(f);
        }
    }

    /// <inheritdoc/>
    public void Load(string path)
    {
        if (!File.Exists(path))
            return;

        _index.Clear();

        using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(fs);

        while (fs.Position < fs.Length)
        {
            var keyLen = reader.ReadInt32();
            var keyBytes = reader.ReadBytes(keyLen);
            var key = System.Text.Encoding.UTF8.GetString(keyBytes);

            var vecLen = reader.ReadInt32();
            var vector = new float[vecLen];
            for (var i = 0; i < vecLen; i++)
                vector[i] = reader.ReadSingle();

            _index[key] = vector;
        }
    }
}
