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
    /// Build the context menu with recent tasks
    /// </summary>
    private void BuildContextMenu()
    {
        var contextMenu = new ContextMenuStrip();

        // Quick run section for recent tasks
        var tasks = _scheduler.GetAllTasks()
            .Where(t => t.Enabled)
            .OrderByDescending(t => t.LastRun ?? DateTime.MinValue)
            .Take(5)
            .ToList();

        if (tasks.Any())
        {
            foreach (var task in tasks)
            {
                var menuItem = new ToolStripMenuItem($"▶ {task.Name}")
                {
                    Tag = task.Id
                };
                menuItem.Click += OnQuickRunTask;
                contextMenu.Items.Add(menuItem);
            }

            contextMenu.Items.Add(new ToolStripSeparator());
        }

        // Standard menu items
        contextMenu.Items.Add("Manage Tasks...", null, OnManageTasks);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Pause All", null, OnPauseAll);
        contextMenu.Items.Add("Resume All", null, OnResumeAll);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, OnExit);

        _notifyIcon.ContextMenuStrip = contextMenu;
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
        // TODO: Open TaskManagerWindow (Task 4)
        ShowNotification("Coming Soon", "Task Manager window will be implemented in Task 4", ToolTipIcon.Info);
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
