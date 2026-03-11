using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hazina.Demo.AgenticOrchestration;

/// <summary>
/// Settings dialog for editing appsettings.json configuration.
/// Allows users to modify key configuration values without manually editing JSON.
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly string _configPath;
    private readonly TabControl _tabControl;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;
    private readonly Button _reloadButton;

    // Terminal Settings
    private TextBox _txtDefaultCommand = null!;
    private TextBox _txtWorkingDirectory = null!;
    private NumericUpDown _numMaxSessions = null!;
    private NumericUpDown _numSessionTimeout = null!;
    private NumericUpDown _numColumns = null!;
    private NumericUpDown _numRows = null!;

    // Authentication Settings
    private CheckBox _chkAuthEnabled = null!;
    private TextBox _txtUsername = null!;
    private TextBox _txtPassword = null!;
    private TextBox _txtRealm = null!;

    // Paths Settings
    private TextBox _txtDatabasePath = null!;
    private TextBox _txtLogsPath = null!;
    private TextBox _txtSessionLogsPath = null!;
    private CheckBox _chkSessionLoggingEnabled = null!;

    // OpenAI Settings
    private TextBox _txtApiKey = null!;
    private TextBox _txtModel = null!;
    private TextBox _txtEmbeddingModel = null!;
    private TextBox _txtImageModel = null!;

    public SettingsForm(string configPath)
    {
        _configPath = configPath;

        // Form properties
        Text = "Hazina Orchestration - Settings";
        Size = new Size(700, 600);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;

        // Main layout
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

        // Tab control for organized settings
        _tabControl = new TabControl { Dock = DockStyle.Fill };
        _tabControl.Controls.Add(CreateTerminalTab());
        _tabControl.Controls.Add(CreateAuthenticationTab());
        _tabControl.Controls.Add(CreatePathsTab());
        _tabControl.Controls.Add(CreateOpenAITab());

        mainPanel.Controls.Add(_tabControl, 0, 0);

        // Button panel
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0)
        };

        _saveButton = new Button
        {
            Text = "Save",
            Size = new Size(100, 30),
            DialogResult = DialogResult.OK
        };
        _saveButton.Click += OnSave;

        _cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(100, 30),
            DialogResult = DialogResult.Cancel
        };

        _reloadButton = new Button
        {
            Text = "Reload",
            Size = new Size(100, 30)
        };
        _reloadButton.Click += (s, e) => LoadConfiguration();

        buttonPanel.Controls.Add(_saveButton);
        buttonPanel.Controls.Add(_cancelButton);
        buttonPanel.Controls.Add(_reloadButton);

        mainPanel.Controls.Add(buttonPanel, 0, 1);

        Controls.Add(mainPanel);

        // Load current configuration
        LoadConfiguration();
    }

    private TabPage CreateTerminalTab()
    {
        var tab = new TabPage("Terminal");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(10),
            AutoScroll = true
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        int row = 0;

        // Default Command
        panel.Controls.Add(new Label { Text = "Default Command:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _txtDefaultCommand = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_txtDefaultCommand, 1, row++);

        // Working Directory
        panel.Controls.Add(new Label { Text = "Working Directory:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _txtWorkingDirectory = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_txtWorkingDirectory, 1, row++);

        // Max Concurrent Sessions
        panel.Controls.Add(new Label { Text = "Max Concurrent Sessions:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _numMaxSessions = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 1, Maximum = 100, Value = 10 };
        panel.Controls.Add(_numMaxSessions, 1, row++);

        // Session Timeout
        panel.Controls.Add(new Label { Text = "Session Timeout (minutes):", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _numSessionTimeout = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 1, Maximum = 1440, Value = 60 };
        panel.Controls.Add(_numSessionTimeout, 1, row++);

        // Default Columns
        panel.Controls.Add(new Label { Text = "Terminal Columns:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _numColumns = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 40, Maximum = 300, Value = 120 };
        panel.Controls.Add(_numColumns, 1, row++);

        // Default Rows
        panel.Controls.Add(new Label { Text = "Terminal Rows:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _numRows = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 10, Maximum = 100, Value = 30 };
        panel.Controls.Add(_numRows, 1, row++);

        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage CreateAuthenticationTab()
    {
        var tab = new TabPage("Authentication");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(10),
            AutoScroll = true
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        int row = 0;

        // Enabled
        panel.Controls.Add(new Label { Text = "Enable Authentication:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _chkAuthEnabled = new CheckBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_chkAuthEnabled, 1, row++);

        // Username
        panel.Controls.Add(new Label { Text = "Username:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _txtUsername = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_txtUsername, 1, row++);

        // Password
        panel.Controls.Add(new Label { Text = "Password:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _txtPassword = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        panel.Controls.Add(_txtPassword, 1, row++);

        // Realm
        panel.Controls.Add(new Label { Text = "Realm:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _txtRealm = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_txtRealm, 1, row++);

        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage CreatePathsTab()
    {
        var tab = new TabPage("Paths");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(10),
            AutoScroll = true
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        int row = 0;

        // Database Path
        panel.Controls.Add(new Label { Text = "Database Path:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _txtDatabasePath = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_txtDatabasePath, 1, row++);

        // Logs Path
        panel.Controls.Add(new Label { Text = "Logs Path:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _txtLogsPath = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_txtLogsPath, 1, row++);

        // Session Logging Enabled
        panel.Controls.Add(new Label { Text = "Enable Session Logging:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _chkSessionLoggingEnabled = new CheckBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_chkSessionLoggingEnabled, 1, row++);

        // Session Logs Path
        panel.Controls.Add(new Label { Text = "Session Logs Path:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _txtSessionLogsPath = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_txtSessionLogsPath, 1, row++);

        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage CreateOpenAITab()
    {
        var tab = new TabPage("OpenAI");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(10),
            AutoScroll = true
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        int row = 0;

        // API Key
        panel.Controls.Add(new Label { Text = "API Key:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _txtApiKey = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        panel.Controls.Add(_txtApiKey, 1, row++);

        // Model
        panel.Controls.Add(new Label { Text = "Model:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _txtModel = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_txtModel, 1, row++);

        // Embedding Model
        panel.Controls.Add(new Label { Text = "Embedding Model:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _txtEmbeddingModel = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_txtEmbeddingModel, 1, row++);

        // Image Model
        panel.Controls.Add(new Label { Text = "Image Model:", TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        _txtImageModel = new TextBox { Dock = DockStyle.Fill };
        panel.Controls.Add(_txtImageModel, 1, row++);

        // Info label
        var infoLabel = new Label
        {
            Text = "Note: Changes require application restart to take effect.",
            ForeColor = System.Drawing.Color.DarkOrange,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0)
        };
        panel.Controls.Add(infoLabel, 0, row);
        panel.SetColumnSpan(infoLabel, 2);

        tab.Controls.Add(panel);
        return tab;
    }

    private void LoadConfiguration()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                MessageBox.Show(
                    $"Configuration file not found: {_configPath}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            var jsonText = File.ReadAllText(_configPath);
            var json = JsonNode.Parse(jsonText) as JsonObject;

            if (json == null) return;

            // Terminal settings
            var terminal = json["AgenticOrchestration"]?["Terminal"] as JsonObject;
            if (terminal != null)
            {
                _txtDefaultCommand.Text = terminal["DefaultCommand"]?.GetValue<string>() ?? "";
                _txtWorkingDirectory.Text = terminal["DefaultWorkingDirectory"]?.GetValue<string>() ?? "";
                _numMaxSessions.Value = terminal["MaxConcurrentSessions"]?.GetValue<int>() ?? 10;
                _numSessionTimeout.Value = terminal["SessionTimeoutMinutes"]?.GetValue<int>() ?? 60;
                _numColumns.Value = terminal["DefaultColumns"]?.GetValue<int>() ?? 120;
                _numRows.Value = terminal["DefaultRows"]?.GetValue<int>() ?? 30;
            }

            // Authentication settings
            var auth = json["Authentication"] as JsonObject;
            if (auth != null)
            {
                _chkAuthEnabled.Checked = auth["Enabled"]?.GetValue<bool>() ?? true;
                _txtUsername.Text = auth["Username"]?.GetValue<string>() ?? "";
                _txtPassword.Text = auth["Password"]?.GetValue<string>() ?? "";
                _txtRealm.Text = auth["Realm"]?.GetValue<string>() ?? "";
            }

            // Paths settings
            var orchestration = json["AgenticOrchestration"] as JsonObject;
            if (orchestration != null)
            {
                _txtDatabasePath.Text = orchestration["DatabasePath"]?.GetValue<string>() ?? "";
                _txtLogsPath.Text = orchestration["LogsPath"]?.GetValue<string>() ?? "";

                var sessionLogging = orchestration["SessionLogging"] as JsonObject;
                if (sessionLogging != null)
                {
                    _chkSessionLoggingEnabled.Checked = sessionLogging["Enabled"]?.GetValue<bool>() ?? true;
                    _txtSessionLogsPath.Text = sessionLogging["BasePath"]?.GetValue<string>() ?? "";
                }
            }

            // OpenAI settings
            var openAI = json["OpenAI"] as JsonObject;
            if (openAI != null)
            {
                _txtApiKey.Text = openAI["ApiKey"]?.GetValue<string>() ?? "";
                _txtModel.Text = openAI["Model"]?.GetValue<string>() ?? "";
                _txtEmbeddingModel.Text = openAI["EmbeddingModel"]?.GetValue<string>() ?? "";
                _txtImageModel.Text = openAI["ImageModel"]?.GetValue<string>() ?? "";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error loading configuration: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OnSave(object? sender, EventArgs e)
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                MessageBox.Show(
                    $"Configuration file not found: {_configPath}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            var jsonText = File.ReadAllText(_configPath);
            var json = JsonNode.Parse(jsonText) as JsonObject;

            if (json == null)
            {
                MessageBox.Show("Invalid JSON configuration file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Update Terminal settings
            var orchestration = json["AgenticOrchestration"] as JsonObject;
            if (orchestration != null)
            {
                var terminal = orchestration["Terminal"] as JsonObject;
                if (terminal != null)
                {
                    terminal["DefaultCommand"] = _txtDefaultCommand.Text;
                    terminal["DefaultWorkingDirectory"] = _txtWorkingDirectory.Text;
                    terminal["MaxConcurrentSessions"] = (int)_numMaxSessions.Value;
                    terminal["SessionTimeoutMinutes"] = (int)_numSessionTimeout.Value;
                    terminal["DefaultColumns"] = (int)_numColumns.Value;
                    terminal["DefaultRows"] = (int)_numRows.Value;
                }

                // Update Paths
                orchestration["DatabasePath"] = _txtDatabasePath.Text;
                orchestration["LogsPath"] = _txtLogsPath.Text;

                var sessionLogging = orchestration["SessionLogging"] as JsonObject;
                if (sessionLogging != null)
                {
                    sessionLogging["Enabled"] = _chkSessionLoggingEnabled.Checked;
                    sessionLogging["BasePath"] = _txtSessionLogsPath.Text;
                }
            }

            // Update Authentication settings
            var auth = json["Authentication"] as JsonObject;
            if (auth != null)
            {
                auth["Enabled"] = _chkAuthEnabled.Checked;
                auth["Username"] = _txtUsername.Text;
                auth["Password"] = _txtPassword.Text;
                auth["Realm"] = _txtRealm.Text;
            }

            // Update OpenAI settings
            var openAI = json["OpenAI"] as JsonObject;
            if (openAI != null)
            {
                openAI["ApiKey"] = _txtApiKey.Text;
                openAI["Model"] = _txtModel.Text;
                openAI["EmbeddingModel"] = _txtEmbeddingModel.Text;
                openAI["ImageModel"] = _txtImageModel.Text;
            }

            // Write back to file with pretty formatting
            var options = new JsonSerializerOptions { WriteIndented = true };
            var updatedJson = JsonSerializer.Serialize(json, options);
            File.WriteAllText(_configPath, updatedJson);

            MessageBox.Show(
                "Settings saved successfully. Restart the application for changes to take effect.",
                "Success",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error saving configuration: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
