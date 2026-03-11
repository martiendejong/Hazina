using System;
using System.IO;
using System.Windows;
using Cronos;
using Hazina.TaskRunner.Scheduling;
using Microsoft.Win32;

namespace Hazina.TaskRunner.UI;

/// <summary>
/// Interaction logic for TaskEditorDialog.xaml
/// </summary>
public partial class TaskEditorDialog : Window
{
    public ScheduledTask? Task { get; private set; }

    private readonly ScheduledTask? _originalTask;

    public TaskEditorDialog(ScheduledTask? task)
    {
        InitializeComponent();

        _originalTask = task;

        if (task != null)
        {
            // Edit mode
            Title = "Edit Task";
            TxtName.Text = task.Name;
            TxtScriptPath.Text = task.ScriptPath;
            TxtCronExpression.Text = task.CronExpression;
            ChkEnabled.IsChecked = task.Enabled;
            ChkElevated.IsChecked = task.RunElevated;
            TxtTimeout.Text = task.TimeoutSeconds.ToString();
        }
        else
        {
            // Add mode
            Title = "Add Task";
            ChkEnabled.IsChecked = true;
            TxtTimeout.Text = "300";
        }
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PowerShell Scripts (*.ps1)|*.ps1|All Files (*.*)|*.*",
            Title = "Select PowerShell Script"
        };

        if (dialog.ShowDialog() == true)
        {
            TxtScriptPath.Text = dialog.FileName;
        }
    }

    private void TxtCronExpression_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        try
        {
            var expr = TxtCronExpression.Text.Trim();
            if (string.IsNullOrEmpty(expr))
            {
                TxtCronPreview.Text = "";
                return;
            }

            DateTime? nextRun = null;

            try
            {
                // Try standard format first
                var cronExpr = CronExpression.Parse(expr, CronFormat.Standard);
                nextRun = cronExpr.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);
            }
            catch (CronFormatException)
            {
                // Try with seconds
                var cronExpr = CronExpression.Parse(expr, CronFormat.IncludeSeconds);
                nextRun = cronExpr.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);
            }

            if (nextRun.HasValue)
            {
                TxtCronPreview.Text = $"Next: {nextRun.Value:yyyy-MM-dd HH:mm}";
                TxtCronPreview.Foreground = System.Windows.Media.Brushes.Green;
            }
        }
        catch
        {
            TxtCronPreview.Text = "Invalid";
            TxtCronPreview.Foreground = System.Windows.Media.Brushes.Red;
        }
    }

    private void BtnOK_Click(object sender, RoutedEventArgs e)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            MessageBox.Show("Please enter a task name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtScriptPath.Text))
        {
            MessageBox.Show("Please enter a script path.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!File.Exists(TxtScriptPath.Text))
        {
            var result = MessageBox.Show(
                $"Script file does not exist: {TxtScriptPath.Text}\n\nDo you want to continue anyway?",
                "File Not Found",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
                return;
        }

        if (string.IsNullOrWhiteSpace(TxtCronExpression.Text))
        {
            MessageBox.Show("Please enter a cron expression.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Validate cron expression
        try
        {
            var expr = TxtCronExpression.Text.Trim();
            try
            {
                CronExpression.Parse(expr, CronFormat.Standard);
            }
            catch (CronFormatException)
            {
                CronExpression.Parse(expr, CronFormat.IncludeSeconds);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Invalid cron expression: {ex.Message}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(TxtTimeout.Text, out int timeout) || timeout <= 0)
        {
            MessageBox.Show("Timeout must be a positive integer.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Create task
        Task = new ScheduledTask
        {
            Id = _originalTask?.Id ?? Guid.NewGuid().ToString(),
            Name = TxtName.Text.Trim(),
            ScriptPath = TxtScriptPath.Text.Trim(),
            CronExpression = TxtCronExpression.Text.Trim(),
            Enabled = ChkEnabled.IsChecked ?? true,
            RunElevated = ChkElevated.IsChecked ?? false,
            TimeoutSeconds = timeout,
            LastRun = _originalTask?.LastRun,
            NextRun = _originalTask?.NextRun
        };

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
