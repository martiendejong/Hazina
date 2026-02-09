using Microsoft.Win32;

namespace Hazina.Demo.AgenticOrchestration.Startup;

/// <summary>
/// Manages auto-start registration via the Windows Registry (HKCU\...\Run).
/// Runs under the current user context so no elevation is required.
/// </summary>
public static class AutoStartHelper
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "HazinaOrchestration";

    /// <summary>
    /// Registers the current executable to start automatically at Windows login.
    /// </summary>
    public static void EnableAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
            var exePath = Environment.ProcessPath;
            if (key != null && exePath != null)
            {
                key.SetValue(AppName, $"\"{exePath}\"");
            }
        }
        catch
        {
            // Silently fail - non-critical feature
        }
    }

    /// <summary>
    /// Removes the auto-start registration.
    /// </summary>
    public static void DisableAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);
        }
        catch
        {
            // Silently fail
        }
    }

    /// <summary>
    /// Checks whether auto-start is currently enabled.
    /// </summary>
    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }
}
