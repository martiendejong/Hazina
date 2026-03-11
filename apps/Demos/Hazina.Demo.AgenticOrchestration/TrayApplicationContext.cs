using System.Diagnostics;
using System.Reflection;
using Hazina.Demo.AgenticOrchestration.Startup;

namespace Hazina.Demo.AgenticOrchestration;

/// <summary>
/// WinForms ApplicationContext that hosts a system tray icon for the Hazina Orchestration service.
/// The ASP.NET Core web host runs on a background thread while the tray icon provides user interaction.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly WebApplication _webApp;
    private readonly CancellationTokenSource _cts = new();
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly string _baseUrl;

    public TrayApplicationContext(WebApplication webApp)
    {
        _webApp = webApp;

        // Resolve the actual base URL from the running web host
        _baseUrl = webApp.Urls.FirstOrDefault(u => u.StartsWith("https://"))
                   ?? webApp.Urls.FirstOrDefault()
                   ?? "https://localhost:5123";

        // Normalize 0.0.0.0 and [::] to localhost for browser URLs
        _baseUrl = _baseUrl.Replace("://0.0.0.0:", "://localhost:")
                           .Replace("://[::]:", "://localhost:");

        // Load icon from embedded resource, fall back to system icon
        var icon = LoadEmbeddedIcon() ?? SystemIcons.Application;

        // Build context menu
        _autoStartItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = AutoStartHelper.IsAutoStartEnabled(),
            CheckOnClick = true
        };
        _autoStartItem.Click += OnAutoStartToggle;

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Open Dashboard", null, OnOpenDashboard);
        contextMenu.Items.Add("Swagger API", null, OnOpenSwagger);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Settings...", null, OnOpenSettings);
        contextMenu.Items.Add(_autoStartItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, OnExit);

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "Hazina Orchestration",
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        _notifyIcon.DoubleClick += OnOpenDashboard;

        // Show a balloon tip on startup with the actual URL
        _notifyIcon.ShowBalloonTip(
            2000,
            "Hazina Orchestration",
            $"Running on {_baseUrl}",
            ToolTipIcon.Info);
    }

    private static Icon? LoadEmbeddedIcon()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream("Hazina.Demo.AgenticOrchestration.app.ico");
            return stream != null ? new Icon(stream) : null;
        }
        catch
        {
            return null;
        }
    }

    private void OnOpenDashboard(object? sender, EventArgs e)
    {
        OpenUrl(_baseUrl);
    }

    private void OnOpenSwagger(object? sender, EventArgs e)
    {
        OpenUrl($"{_baseUrl}/swagger");
    }

    private void OnOpenSettings(object? sender, EventArgs e)
    {
        try
        {
            // Resolve config path from the application directory
            var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

            if (!File.Exists(configPath))
            {
                MessageBox.Show(
                    $"Configuration file not found at: {configPath}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            using var settingsForm = new SettingsForm(configPath);
            settingsForm.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error opening settings: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OnAutoStartToggle(object? sender, EventArgs e)
    {
        if (_autoStartItem.Checked)
            AutoStartHelper.EnableAutoStart();
        else
            AutoStartHelper.DisableAutoStart();
    }

    private async void OnExit(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;

        try
        {
            await _webApp.StopAsync(_cts.Token);
        }
        catch
        {
            // Best-effort shutdown
        }

        _cts.Cancel();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _cts.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Ignore if browser can't open
        }
    }
}
