using System.Text.Json.Nodes;
using Hazina.Demo.AgenticOrchestration.ConfigTool.Models;
using Hazina.Demo.AgenticOrchestration.ConfigTool.Services;

namespace Hazina.Demo.AgenticOrchestration.ConfigTool.Forms;

public class MainForm : Form
{
    private readonly string _configPath;
    private readonly ConfigurationService _configService = new();
    private readonly ValidationService _validationService = new();

    // Authentication controls
    private CheckBox _authEnabled = null!;
    private TextBox _authUsername = null!;
    private TextBox _authPassword = null!;
    private TextBox _authRealm = null!;

    // Web Server controls
    private TextBox _host = null!;
    private NumericUpDown _port = null!;
    private CheckBox _useSsl = null!;
    private TextBox _certPath = null!;
    private Button _certBrowse = null!;
    private TextBox _keyPath = null!;
    private Button _keyBrowse = null!;
    private Label _certLabel = null!;
    private Label _keyLabel = null!;

    // Terminal controls
    private TextBox _termCommand = null!;
    private Button _termCommandBrowse = null!;
    private TextBox _termWorkDir = null!;
    private Button _termWorkDirBrowse = null!;
    private NumericUpDown _termColumns = null!;
    private NumericUpDown _termRows = null!;
    private NumericUpDown _termMaxSessions = null!;
    private NumericUpDown _termTimeout = null!;

    // OpenAI controls
    private TextBox _openaiKey = null!;
    private TextBox _openaiModel = null!;

    // Validation panel
    private TextBox _validationOutput = null!;

    public MainForm(string configPath)
    {
        _configPath = configPath;
        InitializeComponent();
        LoadConfiguration();
    }

    private void InitializeComponent()
    {
        Text = "Hazina Configuration Tool";
        Size = new Size(520, 700);
        MinimumSize = new Size(480, 600);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScroll = true;
        Font = new Font("Segoe UI", 9F);

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(12)
        };
        Controls.Add(panel);

        int y = 8;

        // --- Authentication GroupBox ---
        var authGroup = CreateGroupBox("Authentication", panel, ref y, 130);

        _authEnabled = new CheckBox { Text = "Enabled", Location = new Point(12, 22), AutoSize = true, Checked = true };
        authGroup.Controls.Add(_authEnabled);

        AddLabelAndTextBox(authGroup, "Username:", 48, out _authUsername);
        AddLabelAndTextBox(authGroup, "Password:", 76, out _authPassword);
        _authPassword.UseSystemPasswordChar = true;
        AddLabelAndTextBox(authGroup, "Realm:", 104, out _authRealm);

        // --- Web Server GroupBox ---
        var webGroup = CreateGroupBox("Web Server", panel, ref y, 158);

        AddLabel(webGroup, "Host:", 22, 12);
        _host = new TextBox { Location = new Point(80, 19), Width = 150 };
        webGroup.Controls.Add(_host);

        AddLabel(webGroup, "Port:", 22, 246);
        _port = new NumericUpDown { Location = new Point(282, 19), Width = 70, Minimum = 1, Maximum = 65535, Value = 5123 };
        webGroup.Controls.Add(_port);

        _useSsl = new CheckBox { Text = "SSL", Location = new Point(362, 21), AutoSize = true };
        _useSsl.CheckedChanged += UseSsl_CheckedChanged;
        webGroup.Controls.Add(_useSsl);

        _certLabel = AddLabel(webGroup, "Cert:", 52, 12);
        _certPath = new TextBox { Location = new Point(80, 49), Width = 310 };
        webGroup.Controls.Add(_certPath);
        _certBrowse = new Button { Text = "...", Location = new Point(396, 48), Width = 30, Height = 23 };
        _certBrowse.Click += (_, _) => BrowseFile(_certPath, "Certificate files|*.pem;*.crt;*.pfx|All files|*.*");
        webGroup.Controls.Add(_certBrowse);

        _keyLabel = AddLabel(webGroup, "Key:", 80, 12);
        _keyPath = new TextBox { Location = new Point(80, 77), Width = 310 };
        webGroup.Controls.Add(_keyPath);
        _keyBrowse = new Button { Text = "...", Location = new Point(396, 76), Width = 30, Height = 23 };
        _keyBrowse.Click += (_, _) => BrowseFile(_keyPath, "Key files|*.pem;*.key|All files|*.*");
        webGroup.Controls.Add(_keyBrowse);

        SetCertFieldsVisible(false);

        // --- Terminal GroupBox ---
        var termGroup = CreateGroupBox("Terminal", panel, ref y, 158);

        AddLabel(termGroup, "Command:", 22, 12);
        _termCommand = new TextBox { Location = new Point(100, 19), Width = 290 };
        termGroup.Controls.Add(_termCommand);
        _termCommandBrowse = new Button { Text = "...", Location = new Point(396, 18), Width = 30, Height = 23 };
        _termCommandBrowse.Click += (_, _) => BrowseFile(_termCommand, "Executables|*.exe;*.bat;*.cmd|All files|*.*");
        termGroup.Controls.Add(_termCommandBrowse);

        AddLabel(termGroup, "Work Dir:", 52, 12);
        _termWorkDir = new TextBox { Location = new Point(100, 49), Width = 290 };
        termGroup.Controls.Add(_termWorkDir);
        _termWorkDirBrowse = new Button { Text = "...", Location = new Point(396, 48), Width = 30, Height = 23 };
        _termWorkDirBrowse.Click += (_, _) => BrowseFolder(_termWorkDir);
        termGroup.Controls.Add(_termWorkDirBrowse);

        AddLabel(termGroup, "Columns:", 82, 12);
        _termColumns = new NumericUpDown { Location = new Point(100, 79), Width = 70, Minimum = 1, Maximum = 500, Value = 120 };
        termGroup.Controls.Add(_termColumns);

        AddLabel(termGroup, "Rows:", 82, 190);
        _termRows = new NumericUpDown { Location = new Point(230, 79), Width = 70, Minimum = 1, Maximum = 200, Value = 30 };
        termGroup.Controls.Add(_termRows);

        AddLabel(termGroup, "Max Sessions:", 112, 12);
        _termMaxSessions = new NumericUpDown { Location = new Point(120, 109), Width = 70, Minimum = 1, Maximum = 100, Value = 10 };
        termGroup.Controls.Add(_termMaxSessions);

        AddLabel(termGroup, "Timeout (min):", 112, 210);
        _termTimeout = new NumericUpDown { Location = new Point(310, 109), Width = 70, Minimum = 1, Maximum = 1440, Value = 60 };
        termGroup.Controls.Add(_termTimeout);

        // --- OpenAI GroupBox ---
        var openaiGroup = CreateGroupBox("OpenAI", panel, ref y, 80);

        AddLabel(openaiGroup, "API Key:", 22, 12);
        _openaiKey = new TextBox { Location = new Point(80, 19), Width = 346 };
        openaiGroup.Controls.Add(_openaiKey);

        AddLabel(openaiGroup, "Model:", 52, 12);
        _openaiModel = new TextBox { Location = new Point(80, 49), Width = 200 };
        openaiGroup.Controls.Add(_openaiModel);

        // --- Validation GroupBox ---
        var valGroup = CreateGroupBox("Validation", panel, ref y, 90);

        _validationOutput = new TextBox
        {
            Location = new Point(12, 22),
            Width = 406,
            Height = 58,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = SystemColors.Window,
            Font = new Font("Consolas", 8.5F)
        };
        valGroup.Controls.Add(_validationOutput);

        // --- Buttons ---
        var buttonPanel = new Panel
        {
            Location = new Point(12, y),
            Width = 440,
            Height = 40
        };
        panel.Controls.Add(buttonPanel);

        var validateBtn = new Button { Text = "Validate", Width = 90, Height = 30, Location = new Point(140, 4) };
        validateBtn.Click += ValidateBtn_Click;
        buttonPanel.Controls.Add(validateBtn);

        var saveBtn = new Button { Text = "Save", Width = 90, Height = 30, Location = new Point(240, 4) };
        saveBtn.Click += SaveBtn_Click;
        buttonPanel.Controls.Add(saveBtn);

        var cancelBtn = new Button { Text = "Cancel", Width = 90, Height = 30, Location = new Point(340, 4) };
        cancelBtn.Click += (_, _) => Close();
        buttonPanel.Controls.Add(cancelBtn);

        AcceptButton = saveBtn;
        CancelButton = cancelBtn;
    }

    private GroupBox CreateGroupBox(string text, Panel parent, ref int y, int height)
    {
        var group = new GroupBox
        {
            Text = text,
            Location = new Point(12, y),
            Width = 440,
            Height = height
        };
        parent.Controls.Add(group);
        y += height + 8;
        return group;
    }

    private Label AddLabel(Control parent, string text, int top, int left)
    {
        var label = new Label { Text = text, AutoSize = true, Location = new Point(left, top + 3) };
        parent.Controls.Add(label);
        return label;
    }

    private void AddLabelAndTextBox(Control parent, string labelText, int top, out TextBox textBox)
    {
        AddLabel(parent, labelText, top, 12);
        textBox = new TextBox { Location = new Point(100, top), Width = 326 };
        parent.Controls.Add(textBox);
    }

    private void SetCertFieldsVisible(bool visible)
    {
        _certLabel.Visible = visible;
        _certPath.Visible = visible;
        _certBrowse.Visible = visible;
        _keyLabel.Visible = visible;
        _keyPath.Visible = visible;
        _keyBrowse.Visible = visible;
    }

    private void UseSsl_CheckedChanged(object? sender, EventArgs e)
    {
        SetCertFieldsVisible(_useSsl.Checked);
    }

    private void BrowseFile(TextBox target, string filter)
    {
        using var dlg = new OpenFileDialog { Filter = filter };
        if (!string.IsNullOrEmpty(target.Text) && File.Exists(target.Text))
            dlg.InitialDirectory = Path.GetDirectoryName(target.Text);
        if (dlg.ShowDialog() == DialogResult.OK)
            target.Text = dlg.FileName;
    }

    private void BrowseFolder(TextBox target)
    {
        using var dlg = new FolderBrowserDialog();
        if (!string.IsNullOrEmpty(target.Text) && Directory.Exists(target.Text))
            dlg.InitialDirectory = target.Text;
        if (dlg.ShowDialog() == DialogResult.OK)
            target.Text = dlg.SelectedPath;
    }

    private void LoadConfiguration()
    {
        if (!File.Exists(_configPath))
        {
            _validationOutput.Text = $"Config file not found: {_configPath}\r\nSave will create a new file.";
            return;
        }

        try
        {
            var settings = _configService.ReadConfiguration(_configPath);

            // Authentication
            _authEnabled.Checked = settings.Authentication.Enabled;
            _authUsername.Text = settings.Authentication.Username;
            _authPassword.Text = settings.Authentication.Password;
            _authRealm.Text = settings.Authentication.Realm;

            // Kestrel
            if (settings.Kestrel?.Endpoints.Count > 0)
            {
                var endpoint = settings.Kestrel.Endpoints.Values.First();
                if (Uri.TryCreate(endpoint.Url.Replace("*", "localhost"), UriKind.Absolute, out var uri))
                {
                    _host.Text = uri.Host;
                    _port.Value = Math.Clamp(uri.Port, 1, 65535);
                    _useSsl.Checked = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
                }

                if (endpoint.Certificate != null)
                {
                    _certPath.Text = endpoint.Certificate.Path;
                    _keyPath.Text = endpoint.Certificate.KeyPath;
                }
            }
            else
            {
                _host.Text = "localhost";
                _port.Value = 5123;
            }

            // Terminal
            _termCommand.Text = settings.AgenticOrchestration.Terminal.DefaultCommand;
            _termWorkDir.Text = settings.AgenticOrchestration.Terminal.DefaultWorkingDirectory;
            _termColumns.Value = Math.Clamp(settings.AgenticOrchestration.Terminal.DefaultColumns, 1, 500);
            _termRows.Value = Math.Clamp(settings.AgenticOrchestration.Terminal.DefaultRows, 1, 200);
            _termMaxSessions.Value = Math.Clamp(settings.AgenticOrchestration.Terminal.MaxConcurrentSessions, 1, 100);
            _termTimeout.Value = Math.Clamp(settings.AgenticOrchestration.Terminal.SessionTimeoutMinutes, 1, 1440);

            // OpenAI
            _openaiKey.Text = settings.OpenAI.ApiKey;
            _openaiModel.Text = settings.OpenAI.Model;

            _validationOutput.Text = $"Loaded: {_configPath}";
        }
        catch (Exception ex)
        {
            _validationOutput.Text = $"Error loading config: {ex.Message}";
        }
    }

    private void ValidateBtn_Click(object? sender, EventArgs e)
    {
        try
        {
            var settings = _configService.ReadConfiguration(_configPath);
            var result = _validationService.Validate(settings, _configPath);
            DisplayValidationResult(result);
        }
        catch (Exception ex)
        {
            _validationOutput.Text = $"Validation error: {ex.Message}";
            _validationOutput.ForeColor = Color.Red;
        }
    }

    private void DisplayValidationResult(ValidationResult result)
    {
        var lines = new List<string>();

        foreach (var info in result.Info)
            lines.Add($"[INFO] {info}");
        foreach (var warning in result.Warnings)
            lines.Add($"[WARN] {warning}");
        foreach (var error in result.Errors)
            lines.Add($"[ERR]  {error}");

        lines.Add("");
        lines.Add($"Summary: {result.Errors.Count} errors, {result.Warnings.Count} warnings");
        lines.Add(result.IsValid ? "Status: VALID" : "Status: INVALID");

        _validationOutput.ForeColor = result.IsValid
            ? (result.Warnings.Count > 0 ? Color.DarkGoldenrod : Color.Green)
            : Color.Red;
        _validationOutput.Text = string.Join("\r\n", lines);
    }

    private void SaveBtn_Click(object? sender, EventArgs e)
    {
        try
        {
            JsonNode root;
            if (File.Exists(_configPath))
            {
                root = _configService.ReadAsJsonNode(_configPath);
            }
            else
            {
                root = JsonNode.Parse("{}")!;
            }

            // Authentication
            _configService.UpdateAuthentication(
                root,
                username: _authUsername.Text,
                password: _authPassword.Text,
                realm: _authRealm.Text,
                enabled: _authEnabled.Checked);

            // Kestrel
            var protocol = _useSsl.Checked ? "https" : "http";
            var host = string.IsNullOrWhiteSpace(_host.Text) ? "localhost" : _host.Text;
            var certFile = _useSsl.Checked && !string.IsNullOrWhiteSpace(_certPath.Text) ? _certPath.Text : null;
            var keyFile = _useSsl.Checked && !string.IsNullOrWhiteSpace(_keyPath.Text) ? _keyPath.Text : null;
            _configService.UpdateKestrel(root, protocol, (int)_port.Value, host, certFile, keyFile);

            // Terminal
            _configService.UpdateTerminal(
                root,
                command: _termCommand.Text,
                workingDir: _termWorkDir.Text,
                columns: (int)_termColumns.Value,
                rows: (int)_termRows.Value,
                maxSessions: (int)_termMaxSessions.Value,
                timeout: (int)_termTimeout.Value);

            // OpenAI
            _configService.UpdateOpenAI(
                root,
                apiKey: string.IsNullOrWhiteSpace(_openaiKey.Text) ? null : _openaiKey.Text,
                model: string.IsNullOrWhiteSpace(_openaiModel.Text) ? null : _openaiModel.Text);

            _configService.WriteJsonNode(_configPath, root);

            _validationOutput.ForeColor = Color.Green;
            _validationOutput.Text = $"Saved to: {_configPath}\r\nBackup created.";

            MessageBox.Show("Configuration saved successfully.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _validationOutput.ForeColor = Color.Red;
            _validationOutput.Text = $"Save failed: {ex.Message}";
            MessageBox.Show($"Failed to save configuration:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
