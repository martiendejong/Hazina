using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using static Hazina.AgenticOrchestration.Terminal.ConPty.NativeMethods;

namespace Hazina.AgenticOrchestration.Terminal.ConPty;

/// <summary>
/// Wrapper for a Windows Pseudo Console (ConPTY).
/// Manages the lifecycle of the pseudo console and its associated pipes.
/// </summary>
internal sealed class PseudoConsole : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    public IntPtr Handle => _handle;

    private PseudoConsole(IntPtr handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Creates a new pseudo console with the specified dimensions.
    /// </summary>
    /// <param name="inputReadSide">The read side of the input pipe (from the calling process)</param>
    /// <param name="outputWriteSide">The write side of the output pipe (to the calling process)</param>
    /// <param name="width">Console width in characters</param>
    /// <param name="height">Console height in characters</param>
    /// <returns>A new PseudoConsole instance</returns>
    public static PseudoConsole Create(SafeFileHandle inputReadSide, SafeFileHandle outputWriteSide, short width, short height)
    {
        var size = new COORD(width, height);
        int result = CreatePseudoConsole(size, inputReadSide, outputWriteSide, 0, out IntPtr handle);

        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to create pseudo console. Error code: {result}");
        }

        return new PseudoConsole(handle);
    }

    /// <summary>
    /// Resizes the pseudo console to the specified dimensions.
    /// </summary>
    public void Resize(short width, short height)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PseudoConsole));

        var size = new COORD(width, height);
        int result = ResizePseudoConsole(_handle, size);

        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to resize pseudo console. Error code: {result}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            ClosePseudoConsole(_handle);
            _handle = IntPtr.Zero;
        }
    }
}

/// <summary>
/// Creates and manages anonymous pipes for ConPTY communication.
/// </summary>
internal sealed class PseudoConsolePipe : IDisposable
{
    public SafeFileHandle ReadHandle { get; }
    public SafeFileHandle WriteHandle { get; }

    public PseudoConsolePipe()
    {
        var security = new SECURITY_ATTRIBUTES
        {
            nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
            bInheritHandle = true
        };

        if (!CreatePipe(out var readHandle, out var writeHandle, ref security, BUFFER_SIZE_PIPE))
        {
            throw new InvalidOperationException($"Failed to create pipe. Error: {Marshal.GetLastWin32Error()}");
        }

        ReadHandle = readHandle;
        WriteHandle = writeHandle;
    }

    public void Dispose()
    {
        ReadHandle?.Dispose();
        WriteHandle?.Dispose();
    }
}

/// <summary>
/// Manages the process started within a ConPTY session.
/// </summary>
internal sealed class ConPtyProcess : IDisposable
{
    private PROCESS_INFORMATION _processInfo;
    private bool _disposed;

    public int ProcessId => _processInfo.dwProcessId;
    public IntPtr ProcessHandle => _processInfo.hProcess;

    public bool HasExited
    {
        get
        {
            if (_disposed || _processInfo.hProcess == IntPtr.Zero)
                return true;

            uint exitCode;
            if (!GetExitCodeProcess(_processInfo.hProcess, out exitCode))
                return true;

            return exitCode != STILL_ACTIVE;
        }
    }

    public int? ExitCode
    {
        get
        {
            if (!HasExited)
                return null;

            uint exitCode;
            if (GetExitCodeProcess(_processInfo.hProcess, out exitCode))
                return (int)exitCode;

            return null;
        }
    }

    private ConPtyProcess(PROCESS_INFORMATION processInfo)
    {
        _processInfo = processInfo;
    }

    /// <summary>
    /// Starts a new process attached to the specified pseudo console.
    /// </summary>
    public static ConPtyProcess Start(string command, string? workingDirectory, PseudoConsole pseudoConsole, IDictionary<string, string>? environment = null)
    {
        // Initialize the startup info with pseudo console
        var startupInfo = CreateStartupInfo(pseudoConsole.Handle);

        try
        {
            // Create environment block if needed
            IntPtr envBlock = IntPtr.Zero;
            if (environment != null && environment.Count > 0)
            {
                envBlock = CreateEnvironmentBlock(environment);
            }

            try
            {
                uint creationFlags = EXTENDED_STARTUPINFO_PRESENT;
                if (envBlock != IntPtr.Zero)
                {
                    creationFlags |= CREATE_UNICODE_ENVIRONMENT;
                }

                if (!CreateProcess(
                    null,
                    command,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    creationFlags,
                    envBlock,
                    workingDirectory,
                    ref startupInfo,
                    out PROCESS_INFORMATION processInfo))
                {
                    throw new InvalidOperationException($"Failed to create process. Error: {Marshal.GetLastWin32Error()}");
                }

                return new ConPtyProcess(processInfo);
            }
            finally
            {
                if (envBlock != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(envBlock);
                }
            }
        }
        finally
        {
            // Clean up attribute list
            if (startupInfo.lpAttributeList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(startupInfo.lpAttributeList);
                Marshal.FreeHGlobal(startupInfo.lpAttributeList);
            }
        }
    }

    private static STARTUPINFOEX CreateStartupInfo(IntPtr pseudoConsoleHandle)
    {
        var startupInfo = new STARTUPINFOEX();
        startupInfo.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();

        // Determine the size needed for the attribute list
        IntPtr size = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);

        // Allocate memory for the attribute list
        startupInfo.lpAttributeList = Marshal.AllocHGlobal(size);

        // Initialize the attribute list
        if (!InitializeProcThreadAttributeList(startupInfo.lpAttributeList, 1, 0, ref size))
        {
            Marshal.FreeHGlobal(startupInfo.lpAttributeList);
            throw new InvalidOperationException($"Failed to initialize attribute list. Error: {Marshal.GetLastWin32Error()}");
        }

        // Add the pseudo console attribute
        if (!UpdateProcThreadAttribute(
            startupInfo.lpAttributeList,
            0,
            (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
            pseudoConsoleHandle,
            (IntPtr)IntPtr.Size,
            IntPtr.Zero,
            IntPtr.Zero))
        {
            DeleteProcThreadAttributeList(startupInfo.lpAttributeList);
            Marshal.FreeHGlobal(startupInfo.lpAttributeList);
            throw new InvalidOperationException($"Failed to update attribute. Error: {Marshal.GetLastWin32Error()}");
        }

        return startupInfo;
    }

    private static IntPtr CreateEnvironmentBlock(IDictionary<string, string> environment)
    {
        // Build environment string: "KEY1=VALUE1\0KEY2=VALUE2\0\0"
        var envStrings = environment.Select(kvp => $"{kvp.Key}={kvp.Value}");
        var envBlock = string.Join('\0', envStrings) + "\0\0";

        // Convert to Unicode bytes
        var bytes = System.Text.Encoding.Unicode.GetBytes(envBlock);
        var ptr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);

        return ptr;
    }

    /// <summary>
    /// Waits for the process to exit with an optional timeout.
    /// </summary>
    /// <param name="millisecondsTimeout">Timeout in milliseconds, or -1 for infinite</param>
    /// <returns>True if the process exited, false if timeout occurred</returns>
    public bool WaitForExit(int millisecondsTimeout = -1)
    {
        if (_disposed || _processInfo.hProcess == IntPtr.Zero)
            return true;

        uint timeout = millisecondsTimeout < 0 ? INFINITE : (uint)millisecondsTimeout;
        uint result = WaitForSingleObject(_processInfo.hProcess, timeout);

        return result == WAIT_OBJECT_0;
    }

    /// <summary>
    /// Terminates the process.
    /// </summary>
    public void Kill()
    {
        if (_disposed || _processInfo.hProcess == IntPtr.Zero)
            return;

        TerminateProcess(_processInfo.hProcess, 1);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_processInfo.hThread != IntPtr.Zero)
        {
            CloseHandle(_processInfo.hThread);
            _processInfo.hThread = IntPtr.Zero;
        }

        if (_processInfo.hProcess != IntPtr.Zero)
        {
            CloseHandle(_processInfo.hProcess);
            _processInfo.hProcess = IntPtr.Zero;
        }
    }
}
