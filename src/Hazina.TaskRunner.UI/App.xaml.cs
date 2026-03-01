using System;
using System.IO;
using System.Windows;
using Hazina.TaskRunner.Scheduling;
using WinForms = System.Windows.Forms;

namespace Hazina.TaskRunner.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private SingleInstanceManager? _singleInstance;
    private TrayIconManager? _trayIcon;
    private Hazina.TaskRunner.Scheduling.TaskScheduler? _scheduler;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Enforce single instance
        _singleInstance = new SingleInstanceManager("Hazina.TaskRunner.UI.Mutex");

        if (!_singleInstance.IsFirstInstance)
        {
            MessageBox.Show(
                "Hazina Task Runner is already running. Check the system tray.",
                "Already Running",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Shutdown();
            return;
        }

        // Initialize scheduler with default config path
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Hazina",
            "TaskRunner",
            "tasks.json");

        _scheduler = new Hazina.TaskRunner.Scheduling.TaskScheduler(configPath);

        // Initialize tray icon
        _trayIcon = new TrayIconManager(_scheduler);

        // Show startup notification
        _trayIcon.ShowNotification(
            "Hazina Task Runner",
            "Task Runner started. Right-click tray icon for options.",
            WinForms.ToolTipIcon.Info);

        // Hide main window (we only need tray icon)
        MainWindow = new Window
        {
            Visibility = Visibility.Hidden,
            ShowInTaskbar = false,
            Width = 0,
            Height = 0
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Cleanup
        _trayIcon?.Dispose();
        _scheduler?.Dispose();
        _singleInstance?.Dispose();

        base.OnExit(e);
    }
}

