using System;
using System.Drawing;
using System.Windows.Forms;

namespace WorkTray
{
    public class SettingsForm : Form
    {
        private NumericUpDown _refreshIntervalInput = null!;
        private CheckBox _startWithWindowsCheckbox = null!;
        private CheckBox _showNotificationsCheckbox = null!;
        private Button _saveButton = null!;
        private Button _cancelButton = null!;

        public SettingsForm()
        {
            InitializeComponents();
            LoadSettings();
        }

        private void InitializeComponents()
        {
            Text = "Work Tracker Settings";
            Size = new Size(450, 300);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Refresh interval
            var refreshLabel = new Label
            {
                Text = "Refresh Interval (seconds):",
                Location = new Point(20, 20),
                Size = new Size(200, 20)
            };
            Controls.Add(refreshLabel);

            _refreshIntervalInput = new NumericUpDown
            {
                Location = new Point(220, 18),
                Size = new Size(80, 20),
                Minimum = 1,
                Maximum = 60,
                Value = 3
            };
            Controls.Add(_refreshIntervalInput);

            // Start with Windows
            _startWithWindowsCheckbox = new CheckBox
            {
                Text = "Start with Windows",
                Location = new Point(20, 60),
                Size = new Size(300, 20),
                Checked = IsStartupEnabled()
            };
            Controls.Add(_startWithWindowsCheckbox);

            // Show notifications
            _showNotificationsCheckbox = new CheckBox
            {
                Text = "Show balloon notifications",
                Location = new Point(20, 90),
                Size = new Size(300, 20),
                Checked = true
            };
            Controls.Add(_showNotificationsCheckbox);

            // Info label
            var infoLabel = new Label
            {
                Text = "Settings will take effect after restarting the application.",
                Location = new Point(20, 140),
                Size = new Size(400, 40),
                ForeColor = Color.Gray
            };
            Controls.Add(infoLabel);

            // Buttons
            _saveButton = new Button
            {
                Text = "Save",
                Location = new Point(250, 220),
                Size = new Size(80, 30)
            };
            _saveButton.Click += SaveButton_Click;
            Controls.Add(_saveButton);

            _cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(340, 220),
                Size = new Size(80, 30)
            };
            _cancelButton.Click += (s, e) => Close();
            Controls.Add(_cancelButton);
        }

        private void LoadSettings()
        {
            // TODO: Load from config file
            // For now, use defaults
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // Update startup registry if changed
                if (_startWithWindowsCheckbox.Checked)
                {
                    EnableStartup();
                }
                else
                {
                    DisableStartup();
                }

                // TODO: Save other settings to config file

                MessageBox.Show(
                    "Settings saved successfully.\n\nPlease restart the application for changes to take effect.",
                    "Settings Saved",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to save settings:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private bool IsStartupEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
                return key?.GetValue("WorkTray") != null;
            }
            catch
            {
                return false;
            }
        }

        private void EnableStartup()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

                var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                exePath = exePath.Replace(".dll", ".exe"); // Handle .NET 5+ single-file exe

                key?.SetValue("WorkTray", $"\"{exePath}\"");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to enable startup: {ex.Message}");
            }
        }

        private void DisableStartup()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                key?.DeleteValue("WorkTray", false);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to disable startup: {ex.Message}");
            }
        }
    }
}
