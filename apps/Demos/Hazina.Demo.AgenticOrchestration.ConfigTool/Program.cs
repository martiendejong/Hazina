using System.CommandLine;
using System.Text.Json.Nodes;
using Hazina.Demo.AgenticOrchestration.ConfigTool.Services;

namespace Hazina.Demo.AgenticOrchestration.ConfigTool;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Hazina Configuration Tool - Configure Hazina Agentic Orchestration");

        // Global options
        var configOption = new Option<string>(
            name: "--config",
            description: "Path to appsettings.json",
            getDefaultValue: () => "appsettings.json");

        var verboseOption = new Option<bool>(
            name: "--verbose",
            description: "Show detailed output",
            getDefaultValue: () => false);
        verboseOption.AddAlias("-v");

        var silentOption = new Option<bool>(
            name: "--silent",
            description: "Suppress all output except errors",
            getDefaultValue: () => false);
        silentOption.AddAlias("-s");

        var noBackupOption = new Option<bool>(
            name: "--no-backup",
            description: "Skip automatic backup creation",
            getDefaultValue: () => false);

        rootCommand.AddGlobalOption(configOption);
        rootCommand.AddGlobalOption(verboseOption);
        rootCommand.AddGlobalOption(silentOption);
        rootCommand.AddGlobalOption(noBackupOption);

        // set-auth command
        var setAuthCommand = new Command("set-auth", "Configure authentication (username, password)");
        var usernameOption = new Option<string>("--username", "Username for web UI login") { IsRequired = true };
        var passwordOption = new Option<string>("--password", "Password for web UI login") { IsRequired = true };
        var realmOption = new Option<string?>("--realm", "Authentication realm");
        var enabledOption = new Option<bool?>("--enabled", "Enable/disable authentication");

        setAuthCommand.AddOption(usernameOption);
        setAuthCommand.AddOption(passwordOption);
        setAuthCommand.AddOption(realmOption);
        setAuthCommand.AddOption(enabledOption);

        setAuthCommand.SetHandler(async (configPath, username, password, realm, enabled, verbose, silent, noBackup) =>
        {
            var exitCode = HandleSetAuth(configPath, username, password, realm, enabled, verbose, silent, noBackup);
            Environment.ExitCode = exitCode;
        }, configOption, usernameOption, passwordOption, realmOption, enabledOption, verboseOption, silentOption, noBackupOption);

        // set-kestrel command
        var setKestrelCommand = new Command("set-kestrel", "Configure web server (protocol, port, certificates)");
        var protocolOption = new Option<string>("--protocol", "HTTP or HTTPS") { IsRequired = true };
        var portOption = new Option<int>("--port", () => 5123, "Port number");
        var certOption = new Option<string?>("--cert", "TLS certificate path (required for HTTPS)");
        var keyOption = new Option<string?>("--key", "TLS private key path (required for HTTPS)");

        setKestrelCommand.AddOption(protocolOption);
        setKestrelCommand.AddOption(portOption);
        setKestrelCommand.AddOption(certOption);
        setKestrelCommand.AddOption(keyOption);

        setKestrelCommand.SetHandler(async (configPath, protocol, port, cert, key, verbose, silent, noBackup) =>
        {
            var exitCode = HandleSetKestrel(configPath, protocol, port, cert, key, verbose, silent, noBackup);
            Environment.ExitCode = exitCode;
        }, configOption, protocolOption, portOption, certOption, keyOption, verboseOption, silentOption, noBackupOption);

        // set-paths command
        var setPathsCommand = new Command("set-paths", "Configure file paths (database, logs, uploads)");
        var databaseOption = new Option<string?>("--database", "Database path");
        var logsOption = new Option<string?>("--logs", "Logs directory");
        var uploadsOption = new Option<string?>("--uploads", "Uploads directory");

        setPathsCommand.AddOption(databaseOption);
        setPathsCommand.AddOption(logsOption);
        setPathsCommand.AddOption(uploadsOption);

        setPathsCommand.SetHandler(async (configPath, database, logs, uploads, verbose, silent, noBackup) =>
        {
            var exitCode = HandleSetPaths(configPath, database, logs, uploads, verbose, silent, noBackup);
            Environment.ExitCode = exitCode;
        }, configOption, databaseOption, logsOption, uploadsOption, verboseOption, silentOption, noBackupOption);

        // set-terminal command
        var setTerminalCommand = new Command("set-terminal", "Configure terminal settings");
        var commandOption = new Option<string?>("--command", "Terminal command");
        var workdirOption = new Option<string?>("--workdir", "Working directory");
        var columnsOption = new Option<int?>("--columns", "Terminal columns");
        var rowsOption = new Option<int?>("--rows", "Terminal rows");

        setTerminalCommand.AddOption(commandOption);
        setTerminalCommand.AddOption(workdirOption);
        setTerminalCommand.AddOption(columnsOption);
        setTerminalCommand.AddOption(rowsOption);

        setTerminalCommand.SetHandler(async (configPath, command, workdir, columns, rows, verbose, silent, noBackup) =>
        {
            var exitCode = HandleSetTerminal(configPath, command, workdir, columns, rows, verbose, silent, noBackup);
            Environment.ExitCode = exitCode;
        }, configOption, commandOption, workdirOption, columnsOption, rowsOption, verboseOption, silentOption, noBackupOption);

        // set-openai command
        var setOpenAICommand = new Command("set-openai", "Configure OpenAI settings");
        var apikeyOption = new Option<string?>("--apikey", "OpenAI API key");
        var modelOption = new Option<string?>("--model", "Chat model");
        var clearApikeyOption = new Option<bool>("--clear-apikey", "Remove API key from configuration");

        setOpenAICommand.AddOption(apikeyOption);
        setOpenAICommand.AddOption(modelOption);
        setOpenAICommand.AddOption(clearApikeyOption);

        setOpenAICommand.SetHandler(async (configPath, apikey, model, clearApikey, verbose, silent, noBackup) =>
        {
            var exitCode = HandleSetOpenAI(configPath, apikey, model, clearApikey, verbose, silent, noBackup);
            Environment.ExitCode = exitCode;
        }, configOption, apikeyOption, modelOption, clearApikeyOption, verboseOption, silentOption, noBackupOption);

        // validate command
        var validateCommand = new Command("validate", "Validate configuration");
        validateCommand.SetHandler(async (configPath, verbose, silent) =>
        {
            var exitCode = HandleValidate(configPath, verbose, silent);
            Environment.ExitCode = exitCode;
        }, configOption, verboseOption, silentOption);

        // show command
        var showCommand = new Command("show", "Display current configuration");
        var showSensitiveOption = new Option<bool>("--show-sensitive", "Show sensitive values (passwords, API keys)");
        showCommand.AddOption(showSensitiveOption);

        showCommand.SetHandler(async (configPath, showSensitive, verbose, silent) =>
        {
            var exitCode = HandleShow(configPath, showSensitive, verbose, silent);
            Environment.ExitCode = exitCode;
        }, configOption, showSensitiveOption, verboseOption, silentOption);

        // Add commands to root
        rootCommand.AddCommand(setAuthCommand);
        rootCommand.AddCommand(setKestrelCommand);
        rootCommand.AddCommand(setPathsCommand);
        rootCommand.AddCommand(setTerminalCommand);
        rootCommand.AddCommand(setOpenAICommand);
        rootCommand.AddCommand(validateCommand);
        rootCommand.AddCommand(showCommand);

        return await rootCommand.InvokeAsync(args);
    }

    static int HandleSetAuth(string configPath, string username, string password, string? realm, bool? enabled,
        bool verbose, bool silent, bool noBackup)
    {
        try
        {
            var configService = new ConfigurationService();
            var root = configService.ReadAsJsonNode(configPath);

            if (verbose && !silent)
            {
                Console.WriteLine($"Updating authentication in: {configPath}");
            }

            configService.UpdateAuthentication(root, username, password, realm, enabled);
            configService.WriteJsonNode(configPath, root, !noBackup);

            if (!silent)
            {
                Console.WriteLine("Authentication configured successfully");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine($"Details: {ex.StackTrace}");
            }
            return ex is FileNotFoundException ? 2 : 1;
        }
    }

    static int HandleSetKestrel(string configPath, string protocol, int port, string? cert, string? key,
        bool verbose, bool silent, bool noBackup)
    {
        try
        {
            var configService = new ConfigurationService();
            var root = configService.ReadAsJsonNode(configPath);

            if (verbose && !silent)
            {
                Console.WriteLine($"Updating Kestrel configuration in: {configPath}");
            }

            configService.UpdateKestrel(root, protocol, port, cert, key);
            configService.WriteJsonNode(configPath, root, !noBackup);

            if (!silent)
            {
                Console.WriteLine($"Kestrel configured: {protocol.ToUpper()}://*:{port}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine($"Details: {ex.StackTrace}");
            }
            return ex is FileNotFoundException ? 2 : 1;
        }
    }

    static int HandleSetPaths(string configPath, string? database, string? logs, string? uploads,
        bool verbose, bool silent, bool noBackup)
    {
        try
        {
            var configService = new ConfigurationService();
            var root = configService.ReadAsJsonNode(configPath);

            if (verbose && !silent)
            {
                Console.WriteLine($"Updating file paths in: {configPath}");
            }

            configService.UpdatePaths(root, database, logs, uploads);
            configService.WriteJsonNode(configPath, root, !noBackup);

            if (!silent)
            {
                Console.WriteLine("Paths configured successfully");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine($"Details: {ex.StackTrace}");
            }
            return ex is FileNotFoundException ? 2 : 1;
        }
    }

    static int HandleSetTerminal(string configPath, string? command, string? workdir, int? columns, int? rows,
        bool verbose, bool silent, bool noBackup)
    {
        try
        {
            var configService = new ConfigurationService();
            var root = configService.ReadAsJsonNode(configPath);

            if (verbose && !silent)
            {
                Console.WriteLine($"Updating terminal settings in: {configPath}");
            }

            configService.UpdateTerminal(root, command, workdir, columns, rows);
            configService.WriteJsonNode(configPath, root, !noBackup);

            if (!silent)
            {
                Console.WriteLine("Terminal configured successfully");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine($"Details: {ex.StackTrace}");
            }
            return ex is FileNotFoundException ? 2 : 1;
        }
    }

    static int HandleSetOpenAI(string configPath, string? apikey, string? model, bool clearApikey,
        bool verbose, bool silent, bool noBackup)
    {
        try
        {
            var configService = new ConfigurationService();
            var root = configService.ReadAsJsonNode(configPath);

            if (verbose && !silent)
            {
                Console.WriteLine($"Updating OpenAI settings in: {configPath}");
            }

            configService.UpdateOpenAI(root, apikey, model, clearApiKey: clearApikey);
            configService.WriteJsonNode(configPath, root, !noBackup);

            if (!silent)
            {
                if (clearApikey)
                {
                    Console.WriteLine("OpenAI API key cleared");
                }
                else
                {
                    Console.WriteLine("OpenAI configured successfully");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine($"Details: {ex.StackTrace}");
            }
            return ex is FileNotFoundException ? 2 : 1;
        }
    }

    static int HandleValidate(string configPath, bool verbose, bool silent)
    {
        try
        {
            var configService = new ConfigurationService();
            var validationService = new ValidationService();

            if (verbose && !silent)
            {
                Console.WriteLine($"Validating: {configPath}");
                Console.WriteLine();
            }

            var settings = configService.ReadConfiguration(configPath);
            var result = validationService.Validate(settings, configPath);

            if (!silent)
            {
                // Display results
                foreach (var info in result.Info)
                {
                    Console.WriteLine($"ℹ {info}");
                }

                foreach (var warning in result.Warnings)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"⚠ {warning}");
                    Console.ResetColor();
                }

                foreach (var error in result.Errors)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ {error}");
                    Console.ResetColor();
                }

                Console.WriteLine();
                Console.WriteLine($"Summary: {result.Errors.Count} errors, {result.Warnings.Count} warnings");

                if (result.IsValid)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Status: VALID");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Status: INVALID");
                    Console.ResetColor();
                }
            }

            return result.IsValid ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine($"Details: {ex.StackTrace}");
            }
            return ex is FileNotFoundException ? 2 : 3;
        }
    }

    static int HandleShow(string configPath, bool showSensitive, bool verbose, bool silent)
    {
        try
        {
            var configService = new ConfigurationService();
            var settings = configService.ReadConfiguration(configPath);

            if (silent) return 0;

            Console.WriteLine($"Configuration: {Path.GetFullPath(configPath)}");
            Console.WriteLine();

            // Authentication
            Console.WriteLine("╔════════════════════════════════════════════════════════");
            Console.WriteLine("║ AUTHENTICATION");
            Console.WriteLine("╠════════════════════════════════════════════════════════");
            Console.WriteLine($"║ Enabled:   {settings.Authentication.Enabled}");
            Console.WriteLine($"║ Username:  {settings.Authentication.Username}");
            Console.WriteLine($"║ Password:  {(showSensitive ? settings.Authentication.Password : configService.MaskSensitiveValue(settings.Authentication.Password))}");
            Console.WriteLine($"║ Realm:     {settings.Authentication.Realm}");
            Console.WriteLine();

            // Kestrel
            Console.WriteLine("╔════════════════════════════════════════════════════════");
            Console.WriteLine("║ WEB SERVER (KESTREL)");
            Console.WriteLine("╠════════════════════════════════════════════════════════");
            if (settings.Kestrel != null && settings.Kestrel.Endpoints.Any())
            {
                foreach (var kvp in settings.Kestrel.Endpoints)
                {
                    Console.WriteLine($"║ Endpoint:  {kvp.Key}");
                    Console.WriteLine($"║ URL:       {kvp.Value.Url}");
                    if (kvp.Value.Certificate != null)
                    {
                        Console.WriteLine($"║ Certificate: {kvp.Value.Certificate.Path}");
                        Console.WriteLine($"║ Key:       {kvp.Value.Certificate.KeyPath}");
                    }
                }
            }
            else
            {
                Console.WriteLine("║ No endpoints configured (using defaults)");
            }
            Console.WriteLine();

            // Paths
            Console.WriteLine("╔════════════════════════════════════════════════════════");
            Console.WriteLine("║ FILE PATHS");
            Console.WriteLine("╠════════════════════════════════════════════════════════");
            Console.WriteLine($"║ Database:  {settings.AgenticOrchestration.DatabasePath}");
            Console.WriteLine($"║ Logs:      {settings.AgenticOrchestration.LogsPath}");
            Console.WriteLine($"║ Uploads:   {settings.AgenticOrchestration.Uploads.Path}");
            Console.WriteLine();

            // Terminal
            Console.WriteLine("╔════════════════════════════════════════════════════════");
            Console.WriteLine("║ TERMINAL");
            Console.WriteLine("╠════════════════════════════════════════════════════════");
            Console.WriteLine($"║ Command:   {settings.AgenticOrchestration.Terminal.DefaultCommand}");
            Console.WriteLine($"║ Work Dir:  {settings.AgenticOrchestration.Terminal.DefaultWorkingDirectory}");
            Console.WriteLine($"║ Columns:   {settings.AgenticOrchestration.Terminal.DefaultColumns}");
            Console.WriteLine($"║ Rows:      {settings.AgenticOrchestration.Terminal.DefaultRows}");
            Console.WriteLine($"║ Max Sessions: {settings.AgenticOrchestration.Terminal.MaxConcurrentSessions}");
            Console.WriteLine($"║ Timeout:   {settings.AgenticOrchestration.Terminal.SessionTimeoutMinutes} minutes");
            Console.WriteLine();

            // OpenAI
            Console.WriteLine("╔════════════════════════════════════════════════════════");
            Console.WriteLine("║ OPENAI");
            Console.WriteLine("╠════════════════════════════════════════════════════════");
            var apiKeyDisplay = string.IsNullOrEmpty(settings.OpenAI.ApiKey)
                ? "(not configured)"
                : (showSensitive ? settings.OpenAI.ApiKey : configService.MaskSensitiveValue(settings.OpenAI.ApiKey));
            Console.WriteLine($"║ API Key:   {apiKeyDisplay}");
            Console.WriteLine($"║ Model:     {settings.OpenAI.Model}");
            Console.WriteLine($"║ Embedding: {settings.OpenAI.EmbeddingModel}");
            Console.WriteLine($"║ Image:     {settings.OpenAI.ImageModel}");
            Console.WriteLine($"║ TTS:       {settings.OpenAI.TtsModel}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine($"Details: {ex.StackTrace}");
            }
            return ex is FileNotFoundException ? 2 : 3;
        }
    }
}
