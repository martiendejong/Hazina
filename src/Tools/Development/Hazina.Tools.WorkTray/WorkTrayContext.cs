using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WorkTray
{
    public class WorkTrayContext : ApplicationContext
    {
        private NotifyIcon? _trayIcon;
        private Timer? _updateTimer;
        private readonly string _statePath;
        private readonly string _dashboardPath;
        private WorkState? _currentState;
        private DateTime _lastUpdateTime = DateTime.MinValue;

        // State thresholds
        private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(3);
        private readonly TimeSpan _staleThreshold = TimeSpan.FromMinutes(30);

        public WorkTrayContext()
        {
            _statePath = @"C:\scripts\_machine\work-state.json";
            _dashboardPath = "http://localhost:4242/work-dashboard.html";

            InitializeTrayIcon();
            InitializeTimer();

            // Initial update
            UpdateState();
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new NotifyIcon
            {
                Icon = GetIcon(IconState.Idle),
                Visible = true,
                Text = "Work Tracker - Initializing..."
            };

            // Context menu
            var contextMenu = new ContextMenuStrip();

            contextMenu.Items.Add("Open Dashboard", null, (s, e) => OpenDashboard());
            contextMenu.Items.Add("Open State File", null, (s, e) => OpenStateFile());
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Refresh Now", null, (s, e) => UpdateState());
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Settings", null, (s, e) => ShowSettings());
            contextMenu.Items.Add("About", null, (s, e) => ShowAbout());
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

            _trayIcon.ContextMenuStrip = contextMenu;

            // Double-click opens dashboard
            _trayIcon.DoubleClick += (s, e) => OpenDashboard();

            // Single click shows balloon with summary
            _trayIcon.Click += (s, e) =>
            {
                if (((MouseEventArgs)e).Button == MouseButtons.Left)
                {
                    ShowQuickSummary();
                }
            };
        }

        private void InitializeTimer()
        {
            _updateTimer = new Timer
            {
                Interval = (int)_updateInterval.TotalMilliseconds
            };
            _updateTimer.Tick += (s, e) => UpdateState();
            _updateTimer.Start();
        }

        private void UpdateState()
        {
            try
            {
                if (!File.Exists(_statePath))
                {
                    UpdateTrayIcon(IconState.Idle, "No work state found");
                    return;
                }

                // Check file modification time (avoid re-parsing if unchanged)
                var fileInfo = new FileInfo(_statePath);
                if (fileInfo.LastWriteTime <= _lastUpdateTime)
                {
                    return; // No changes
                }

                _lastUpdateTime = fileInfo.LastWriteTime;

                // Parse JSON
                var json = File.ReadAllText(_statePath);
                var jObject = JObject.Parse(json);

                _currentState = new WorkState
                {
                    ActiveAgents = jObject["summary"]?["active_agents"]?.Value<int>() ?? 0,
                    TotalTasksToday = jObject["summary"]?["total_tasks_today"]?.Value<int>() ?? 0,
                    PRsCreatedToday = jObject["summary"]?["prs_created_today"]?.Value<int>() ?? 0,
                    Agents = new List<AgentInfo>()
                };

                // Parse agents
                var agents = jObject["agents"] as JObject;
                if (agents != null)
                {
                    foreach (var agent in agents.Properties())
                    {
                        var agentData = agent.Value as JObject;
                        if (agentData == null) continue;

                        var status = agentData["status"]?.Value<string>() ?? "UNKNOWN";

                        // Skip DONE and IDLE agents
                        if (status == "DONE" || status == "IDLE") continue;

                        var agentInfo = new AgentInfo
                        {
                            Id = agent.Name,
                            Status = status,
                            Phase = agentData["phase"]?.Value<string>() ?? "",
                            Objective = agentData["task"]?["objective"]?.Value<string>() ?? "Unknown",
                            ClickUpTask = agentData["task"]?["clickup"]?.Value<string>(),
                            PR = agentData["task"]?["pr"]?.Value<string>(),
                            Repository = agentData["task"]?["repository"]?.Value<string>(),
                            Updated = ParseDateTime(agentData["updated"]?.Value<string>())
                        };

                        _currentState.Agents.Add(agentInfo);
                    }
                }

                // Update UI
                UpdateUI();
            }
            catch (Exception ex)
            {
                UpdateTrayIcon(IconState.Error, $"Error: {ex.Message}");
            }
        }

        private void UpdateUI()
        {
            if (_currentState == null)
            {
                UpdateTrayIcon(IconState.Idle, "No active work");
                return;
            }

            var activeCount = _currentState.ActiveAgents;

            // Determine icon state
            IconState iconState;
            if (activeCount == 0)
            {
                iconState = IconState.Idle;
            }
            else if (activeCount <= 2)
            {
                iconState = IconState.Working;
            }
            else
            {
                iconState = IconState.Busy;
            }

            // Check for stale agents
            var now = DateTime.UtcNow;
            var hasStaleWork = _currentState.Agents.Any(a =>
                a.Updated.HasValue && (now - a.Updated.Value) > _staleThreshold);

            if (hasStaleWork)
            {
                iconState = IconState.Warning;
            }

            // Build tooltip
            string tooltip;
            if (activeCount == 0)
            {
                tooltip = "No active work";
            }
            else
            {
                var lines = new List<string>
                {
                    $"{activeCount} agent{(activeCount != 1 ? "s" : "")} working"
                };

                foreach (var agent in _currentState.Agents.Take(3))
                {
                    var elapsed = agent.Updated.HasValue
                        ? FormatElapsed(DateTime.UtcNow - agent.Updated.Value)
                        : "";

                    var line = $"• {agent.Id}: {TruncateString(agent.Objective, 30)}";
                    if (!string.IsNullOrEmpty(elapsed))
                    {
                        line += $" ({elapsed})";
                    }
                    lines.Add(line);
                }

                if (_currentState.Agents.Count > 3)
                {
                    lines.Add($"... and {_currentState.Agents.Count - 3} more");
                }

                // Add stats
                lines.Add("");
                lines.Add($"Today: {_currentState.TotalTasksToday} tasks, {_currentState.PRsCreatedToday} PRs");

                tooltip = string.Join("\n", lines);
            }

            UpdateTrayIcon(iconState, tooltip);
        }

        private void UpdateTrayIcon(IconState state, string tooltip)
        {
            if (_trayIcon == null) return;

            _trayIcon.Icon = GetIcon(state);
            _trayIcon.Text = TruncateString(tooltip, 127); // Tooltip max length
        }

        private Icon GetIcon(IconState state)
        {
            // Try to load custom icon
            var iconPath = state switch
            {
                IconState.Idle => @"icons\idle.ico",
                IconState.Working => @"icons\working.ico",
                IconState.Busy => @"icons\busy.ico",
                IconState.Warning => @"icons\warning.ico",
                IconState.Error => @"icons\error.ico",
                _ => @"icons\idle.ico"
            };

            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, iconPath);

            if (File.Exists(fullPath))
            {
                try
                {
                    return new Icon(fullPath);
                }
                catch
                {
                    // Fall through to generated icon
                }
            }

            // Generate icon programmatically
            return GenerateIcon(state);
        }

        private Icon GenerateIcon(IconState state)
        {
            var bitmap = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Background
                g.Clear(Color.Transparent);

                // Choose color based on state
                Color fillColor = state switch
                {
                    IconState.Idle => Color.Gray,
                    IconState.Working => Color.DodgerBlue,
                    IconState.Busy => Color.LimeGreen,
                    IconState.Warning => Color.Orange,
                    IconState.Error => Color.Red,
                    _ => Color.Gray
                };

                // Draw circle
                using (var brush = new SolidBrush(fillColor))
                {
                    g.FillEllipse(brush, 4, 4, 24, 24);
                }

                // Draw border
                using (var pen = new Pen(Color.White, 2))
                {
                    g.DrawEllipse(pen, 4, 4, 24, 24);
                }
            }

            return Icon.FromHandle(bitmap.GetHicon());
        }

        private void ShowQuickSummary()
        {
            if (_currentState == null || _currentState.ActiveAgents == 0)
            {
                _trayIcon?.ShowBalloonTip(2000, "Work Tracker",
                    "No active work", ToolTipIcon.Info);
                return;
            }

            var message = $"{_currentState.ActiveAgents} agent(s) working\n";
            message += $"Today: {_currentState.TotalTasksToday} tasks, {_currentState.PRsCreatedToday} PRs\n\n";

            var topAgent = _currentState.Agents.FirstOrDefault();
            if (topAgent != null)
            {
                message += $"Latest: {topAgent.Id}\n{topAgent.Objective}";
            }

            _trayIcon?.ShowBalloonTip(5000, "Work Summary", message, ToolTipIcon.Info);
        }

        private void OpenDashboard()
        {
            try
            {
                if (!File.Exists(_dashboardPath))
                {
                    MessageBox.Show(
                        $"Dashboard not found at:\n{_dashboardPath}\n\nPlease ensure the work tracking system is fully installed.",
                        "Dashboard Not Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = _dashboardPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to open dashboard:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OpenStateFile()
        {
            try
            {
                if (!File.Exists(_statePath))
                {
                    MessageBox.Show(
                        $"State file not found at:\n{_statePath}",
                        "File Not Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = _statePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to open state file:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ShowSettings()
        {
            var form = new SettingsForm();
            form.ShowDialog();
        }

        private void ShowAbout()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            MessageBox.Show(
                $"Work Tracker System Tray\n" +
                $"Version {version}\n\n" +
                $"Real-time work tracking and monitoring\n" +
                $"for multi-agent development workflows.\n\n" +
                $"State: {_statePath}\n" +
                $"Dashboard: {_dashboardPath}",
                "About Work Tracker",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ExitApplication()
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
            _trayIcon?.Dispose();
            Application.Exit();
        }

        // Helper methods

        private DateTime? ParseDateTime(string? dateString)
        {
            if (string.IsNullOrEmpty(dateString)) return null;

            if (DateTime.TryParse(dateString, out var result))
            {
                return result.ToUniversalTime();
            }

            return null;
        }

        private string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed.TotalMinutes < 1)
                return "just now";
            if (elapsed.TotalMinutes < 60)
                return $"{(int)elapsed.TotalMinutes}m ago";
            if (elapsed.TotalHours < 24)
                return $"{(int)elapsed.TotalHours}h ago";
            return $"{(int)elapsed.TotalDays}d ago";
        }

        private string TruncateString(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
                return input;

            return input.Substring(0, maxLength - 3) + "...";
        }
    }

    // Data models

    public class WorkState
    {
        public int ActiveAgents { get; set; }
        public int TotalTasksToday { get; set; }
        public int PRsCreatedToday { get; set; }
        public List<AgentInfo> Agents { get; set; } = new();
    }

    public class AgentInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty;
        public string Objective { get; set; } = string.Empty;
        public string? ClickUpTask { get; set; }
        public string? PR { get; set; }
        public string? Repository { get; set; }
        public DateTime? Updated { get; set; }
    }

    public enum IconState
    {
        Idle,
        Working,
        Busy,
        Warning,
        Error
    }
}
