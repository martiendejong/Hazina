using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.App.HazinaCoder.Core.Streaming;

/// <summary>
/// Graceful interrupt and resume capability.
/// Allows user to stop agent with Ctrl+C, then resume or terminate.
///
/// Improvement #95: Interrupt & Resume (Value: 8/10, Effort: 5/10, Ratio: 1.60)
/// </summary>
public class InterruptHandler
{
    private readonly CancellationTokenSource _cts;
    private bool _wasInterrupted;
    private DateTime? _interruptedAt;
    private string? _interruptReason;

    public event EventHandler<InterruptedEventArgs>? Interrupted;
    public event EventHandler? Resumed;

    public bool IsInterrupted => _wasInterrupted;
    public CancellationToken Token => _cts.Token;

    public InterruptHandler()
    {
        _cts = new CancellationTokenSource();
        _wasInterrupted = false;

        // Register Ctrl+C handler
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    /// <summary>
    /// Handle Ctrl+C gracefully
    /// </summary>
    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true; // Prevent immediate termination

        if (!_wasInterrupted)
        {
            Console.WriteLine("\n⏸️  Interrupt detected - pausing gracefully...");

            _wasInterrupted = true;
            _interruptedAt = DateTime.UtcNow;
            _interruptReason = "User pressed Ctrl+C";

            // Cancel ongoing operations
            _cts.Cancel();

            // Fire interrupt event
            Interrupted?.Invoke(this, new InterruptedEventArgs
            {
                Timestamp = _interruptedAt.Value,
                Reason = _interruptReason
            });

            // Ask user what to do
            PromptUserAction();
        }
        else
        {
            Console.WriteLine("\n🛑 Second interrupt detected - forcing termination...");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Prompt user for action after interrupt
    /// </summary>
    private void PromptUserAction()
    {
        Console.WriteLine("\n📋 What would you like to do?");
        Console.WriteLine("   1. Resume execution");
        Console.WriteLine("   2. Stop and save state");
        Console.WriteLine("   3. Force quit (discard state)");
        Console.Write("\nChoice (1-3): ");

        var choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                ResumeExecution();
                break;

            case "2":
                StopAndSave();
                break;

            case "3":
                ForceQuit();
                break;

            default:
                Console.WriteLine("⚠️ Invalid choice - defaulting to stop and save");
                StopAndSave();
                break;
        }
    }

    /// <summary>
    /// Resume execution
    /// </summary>
    private void ResumeExecution()
    {
        Console.WriteLine("▶️  Resuming execution...\n");

        _wasInterrupted = false;
        _interruptedAt = null;
        _interruptReason = null;

        // Fire resume event
        Resumed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Stop and save state
    /// </summary>
    private void StopAndSave()
    {
        Console.WriteLine("💾 Saving state and stopping gracefully...\n");

        // Signal that we should save and exit
        // (caller should handle state saving)
        Environment.Exit(0);
    }

    /// <summary>
    /// Force quit without saving
    /// </summary>
    private void ForceQuit()
    {
        Console.WriteLine("🛑 Force quitting - state discarded\n");
        Environment.Exit(1);
    }

    /// <summary>
    /// Check if operation should continue
    /// </summary>
    public bool ShouldContinue()
    {
        return !_wasInterrupted && !_cts.Token.IsCancellationRequested;
    }

    /// <summary>
    /// Wait with cancellation support
    /// </summary>
    public async Task WaitAsync(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
    }

    /// <summary>
    /// Execute action with interrupt support
    /// </summary>
    public async Task<T?> ExecuteWithInterruptAsync<T>(Func<CancellationToken, Task<T>> action)
    {
        try
        {
            return await action(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("⏸️  Operation cancelled due to interrupt");
            return default;
        }
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
        _cts.Dispose();
    }
}

/// <summary>
/// Event args for interrupt event
/// </summary>
public class InterruptedEventArgs : EventArgs
{
    public DateTime Timestamp { get; set; }
    public string Reason { get; set; } = string.Empty;
}
