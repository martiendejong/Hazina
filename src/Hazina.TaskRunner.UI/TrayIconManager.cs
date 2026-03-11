using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Hazina.TaskRunner.Scheduling;

namespace Hazina.TaskRunner.UI;

/// <summary>
/// Manages the system tray icon, context menu, and notifications
/// </summary>
public class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Hazina.TaskRunner.Scheduling.TaskScheduler _scheduler;
    private bool _disposed;

    public TrayIconManager(Hazina.TaskRunner.Scheduling.TaskScheduler scheduler)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));

        _notifyIcon = new NotifyIcon
        {
            Text = "Hazina Task Runner",
            Visible = true
        };

        // Set initial icon (gray = idle)
        SetIconState(TrayIconState.Idle);

        // Build context menu
        BuildContextMenu();

        // Wire up events
        _notifyIcon.DoubleClick += OnDoubleClick;
    }

    /// <summary>
    /// Update tray icon color based on state
    /// </summary>
    public void SetIconState(TrayIconState state)
    {
        // Create simple colored icon (16x16)
        using var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var color = state switch
            {
                TrayIconState.Running => Color.Green,
                TrayIconState.Warning => Color.Yellow,
                TrayIconState.Error => Color.Red,
                TrayIconState.Idle => Color.Gray,
                _ => Color.Gray
            };

            using (var brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, 2, 2, 12, 12);
            }

            // Add border
            using (var pen = new Pen(Color.Black, 1))
            {
                g.DrawEllipse(pen, 2, 2, 12, 12);
            }
        }

        var iconHandle = bitmap.GetHicon();
        _notifyIcon.Icon = Icon.FromHandle(iconHandle);
    }

    /// <summary>
    /// Show balloon tip notification
    /// </summary>
    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, message, icon);
    }

    /// <summary>
    /// Build the context menu with task sets and tasks
    /// </summary>
    private void BuildContextMenu()
    {
        var contextMenu = new ContextMenuStrip();
        var taskSets = _scheduler.GetAllTaskSets();

        // Build hierarchical menu for each task set
        foreach (var taskSet in taskSets)
        {
            // Create submenu for this task set
            var taskSetMenu = new ToolStripMenuItem(taskSet.Name)
            {
                Tag = taskSet.Id
            };

            // Add checkmark if enabled
            taskSetMenu.Checked = taskSet.Enabled;

            // Add enable/disable toggle
            var toggleItem = new ToolStripMenuItem(taskSet.Enabled ? "✓ Enabled" : "✗ Disabled")
            {
                Tag = taskSet.Id
            };
            toggleItem.Click += OnToggleTaskSet;
            taskSetMenu.DropDownItems.Add(toggleItem);

            // Show task count
            var countItem = new ToolStripMenuItem($"({taskSet.Tasks.Count} tasks)")
            {
                Enabled = false
            };
            taskSetMenu.DropDownItems.Add(countItem);

            // Add separator before task list
            if (taskSet.Tasks.Any())
            {
                taskSetMenu.DropDownItems.Add(new ToolStripSeparator());

                // Add individual tasks (recent 5)
                var recentTasks = taskSet.Tasks
                    .Where(t => t.Enabled)
                    .OrderByDescending(t => t.LastRun ?? DateTime.MinValue)
                    .Take(5)
                    .ToList();

                foreach (var task in recentTasks)
                {
                    var taskItem = new ToolStripMenuItem($"▶ {task.Name}")
                    {
                        Tag = task.Id
                    };
                    taskItem.Click += OnQuickRunTask;
                    taskSetMenu.DropDownItems.Add(taskItem);
                }

                if (recentTasks.Count < taskSet.Tasks.Count)
                {
                    var moreItem = new ToolStripMenuItem($"... {taskSet.Tasks.Count - recentTasks.Count} more")
                    {
                        Enabled = false
                    };
                    taskSetMenu.DropDownItems.Add(moreItem);
                }
            }

            contextMenu.Items.Add(taskSetMenu);
        }

        // Add separator before standard items
        if (taskSets.Any())
        {
            contextMenu.Items.Add(new ToolStripSeparator());
        }

        // Standard menu items
        contextMenu.Items.Add("Manage Tasks...", null, OnManageTasks);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Pause All", null, OnPauseAll);
        contextMenu.Items.Add("Resume All", null, OnResumeAll);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Reload Configuration", null, OnReloadConfig);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, OnExit);

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    /// <summary>
    /// Toggle a task set enabled/disabled
    /// </summary>
    private void OnToggleTaskSet(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem menuItem && menuItem.Tag is string taskSetId)
        {
            var taskSet = _scheduler.GetTaskSet(taskSetId);
            if (taskSet != null)
            {
                if (taskSet.Enabled)
                {
                    _scheduler.DisableTaskSet(taskSetId);
                    ShowNotification("Task Set Disabled", $"{taskSet.Name} paused", ToolTipIcon.Warning);
                }
                else
                {
                    _scheduler.EnableTaskSet(taskSetId);
                    ShowNotification("Task Set Enabled", $"{taskSet.Name} resumed", ToolTipIcon.Info);
                }

                // Refresh menu to show updated state
                RefreshContextMenu();
            }
        }
    }

    /// <summary>
    /// Reload configuration from disk
    /// </summary>
    private void OnReloadConfig(object? sender, EventArgs e)
    {
        try
        {
            _scheduler.ReloadTaskSets();
            RefreshContextMenu();
            ShowNotification("Configuration Reloaded", "Task sets updated from disk", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            ShowNotification("Reload Failed", ex.Message, ToolTipIcon.Error);
        }
    }

    /// <summary>
    /// Refresh context menu (call after tasks change)
    /// </summary>
    public void RefreshContextMenu()
    {
        BuildContextMenu();
    }

    private void OnQuickRunTask(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem menuItem && menuItem.Tag is string taskId)
        {
            try
            {
                SetIconState(TrayIconState.Running);
                _scheduler.RunTaskNow(taskId);
                var taskName = menuItem.Text?.TrimStart('▶', ' ') ?? "Unknown";
                ShowNotification("Task Started", $"Running task: {taskName}", ToolTipIcon.Info);

                // Reset icon after 2 seconds
                Task.Delay(2000).ContinueWith(_ => SetIconState(TrayIconState.Idle));
            }
            catch (Exception ex)
            {
                SetIconState(TrayIconState.Error);
                ShowNotification("Task Failed", ex.Message, ToolTipIcon.Error);
            }
        }
    }

    private void OnManageTasks(object? sender, EventArgs e)
    {
        // Open TaskManagerWindow
        var window = new TaskManagerWindow(_scheduler);
        window.Show();
        window.Activate();
    }

    private void OnPauseAll(object? sender, EventArgs e)
    {
        var tasks = _scheduler.GetAllTasks().Where(t => t.Enabled).ToList();
        foreach (var task in tasks)
        {
            _scheduler.DisableTask(task.Id);
        }

        SetIconState(TrayIconState.Warning);
        ShowNotification("Tasks Paused", $"{tasks.Count} task(s) paused", ToolTipIcon.Warning);
    }

    private void OnResumeAll(object? sender, EventArgs e)
    {
        var tasks = _scheduler.GetAllTasks().Where(t => !t.Enabled).ToList();
        foreach (var task in tasks)
        {
            _scheduler.EnableTask(task.Id);
        }

        SetIconState(TrayIconState.Idle);
        ShowNotification("Tasks Resumed", $"{tasks.Count} task(s) resumed", ToolTipIcon.Info);
    }

    private void OnDoubleClick(object? sender, EventArgs e)
    {
        // Double-click opens task manager
        OnManageTasks(sender, e);
    }

    private void OnExit(object? sender, EventArgs e)
    {
        // Trigger application shutdown
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _notifyIcon?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Tray icon color states
/// </summary>
public enum TrayIconState
{
    Idle,      // Gray - no tasks running
    Running,   // Green - task executing
    Warning,   // Yellow - tasks paused
    Error      // Red - task failed
}
