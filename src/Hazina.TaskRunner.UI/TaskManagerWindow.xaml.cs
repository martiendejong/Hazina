using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Hazina.TaskRunner.Scheduling;

namespace Hazina.TaskRunner.UI;

/// <summary>
/// Interaction logic for TaskManagerWindow.xaml
/// </summary>
public partial class TaskManagerWindow : Window
{
    private readonly Hazina.TaskRunner.Scheduling.TaskScheduler _scheduler;

    public TaskManagerWindow(Hazina.TaskRunner.Scheduling.TaskScheduler scheduler)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        InitializeComponent();

        LoadTasks();
    }

    private void LoadTasks()
    {
        var tasks = _scheduler.GetAllTasks();
        TasksGrid.ItemsSource = tasks;
        TaskCountText.Text = $"{tasks.Count} task(s)";
        StatusText.Text = "Ready";
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new TaskEditorDialog(null);
        if (dialog.ShowDialog() == true && dialog.Task != null)
        {
            // Get or create default task set for backward compatibility
            var taskSetId = GetOrCreateDefaultTaskSet();
            _scheduler.AddTask(taskSetId, dialog.Task);
            LoadTasks();
            StatusText.Text = $"Task '{dialog.Task.Name}' added";
        }
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (TasksGrid.SelectedItem is ScheduledTask selectedTask)
        {
            var dialog = new TaskEditorDialog(selectedTask);
            if (dialog.ShowDialog() == true && dialog.Task != null)
            {
                // Find which task set contains this task
                var taskSetId = FindTaskSetForTask(dialog.Task.Id);
                _scheduler.UpdateTask(taskSetId, dialog.Task);
                LoadTasks();
                StatusText.Text = $"Task '{dialog.Task.Name}' updated";
            }
        }
    }

    /// <summary>
    /// Get or create the default task set
    /// </summary>
    private string GetOrCreateDefaultTaskSet()
    {
        // Try to find existing default task set
        var defaultTaskSet = _scheduler.GetTaskSet("default");
        if (defaultTaskSet != null)
        {
            return "default";
        }

        // If no task sets exist, assume legacy mode and use first available
        var taskSets = _scheduler.GetAllTaskSets();
        if (taskSets.Any())
        {
            return taskSets[0].Id;
        }

        // This shouldn't happen, but return "default" as fallback
        return "default";
    }

    /// <summary>
    /// Find which task set contains a specific task
    /// </summary>
    private string FindTaskSetForTask(string taskId)
    {
        var taskSets = _scheduler.GetAllTaskSets();
        foreach (var taskSet in taskSets)
        {
            if (taskSet.Tasks.Any(t => t.Id == taskId))
            {
                return taskSet.Id;
            }
        }

        // Fallback to default
        return GetOrCreateDefaultTaskSet();
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (TasksGrid.SelectedItem is ScheduledTask selectedTask)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to delete task '{selectedTask.Name}'?",
                "Delete Task",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _scheduler.RemoveTask(selectedTask.Id);
                LoadTasks();
                StatusText.Text = $"Task '{selectedTask.Name}' deleted";
            }
        }
    }

    private void BtnRunNow_Click(object sender, RoutedEventArgs e)
    {
        if (TasksGrid.SelectedItem is ScheduledTask selectedTask)
        {
            try
            {
                StatusText.Text = $"Running task '{selectedTask.Name}'...";
                _scheduler.RunTaskNow(selectedTask.Id);
                LoadTasks();
                StatusText.Text = $"Task '{selectedTask.Name}' executed";

                MessageBox.Show(
                    $"Task '{selectedTask.Name}' has been executed.\n\nCheck the execution log for details.",
                    "Task Executed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to run task: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText.Text = "Error running task";
            }
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        LoadTasks();
    }
}

/// <summary>
/// Converter to check if value is not null
/// </summary>
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value != null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
