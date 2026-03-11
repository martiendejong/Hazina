# HazinaConfigTool - Design Specification

**Version:** 1.0
**Date:** 2026-03-01
**Purpose:** Standalone configuration tool for Hazina Agentic Orchestration

---

## Overview

HazinaConfigTool is a command-line utility for managing the configuration of Hazina Agentic Orchestration. It replaces the complex inline PowerShell configuration logic with a dedicated, testable, reusable tool.

### Design Goals

1. **Single Purpose:** Configure appsettings.json and appsettings.Production.json
2. **Fail-Safe:** Always backup before modification, rollback on error
3. **Scriptable:** Non-interactive mode for automation and MSI integration
4. **User-Friendly:** Clear error messages, helpful defaults
5. **Testable:** Unit tests for all configuration operations
6. **Platform:** Windows-first, but .NET 9.0 for future cross-platform support

---

## Technology Stack

- **Language:** C# 13
- **Framework:** .NET 9.0 (console application)
- **CLI Framework:** System.CommandLine 2.0.0-beta4.22272.1
- **JSON Library:** System.Text.Json (built-in, high performance)
- **Configuration:** Microsoft.Extensions.Configuration (for appsettings.json handling)
- **Testing:** xUnit + FluentAssertions
- **Output:** Single-file executable (PublishSingleFile=true)

---

## Project Structure

```
Hazina.Demo.AgenticOrchestration.ConfigTool/
├── Program.cs                          # Entry point, CLI command registration
├── Commands/
│   ├── SetAuthCommand.cs               # set-auth command
│   ├── SetKestrelCommand.cs            # set-kestrel command
│   ├── SetPathsCommand.cs              # set-paths command
│   ├── SetTerminalCommand.cs           # set-terminal command
│   ├── SetOpenAICommand.cs             # set-openai command
│   ├── ValidateCommand.cs              # validate command
│   ├── ShowCommand.cs                  # show command
│   ├── BackupCommand.cs                # backup command
│   └── RestoreCommand.cs               # restore command
├── Services/
│   ├── ConfigurationService.cs         # Core config read/write logic
│   ├── ValidationService.cs            # Configuration validation
│   ├── BackupService.cs                # Backup/restore operations
│   └── DisplayService.cs               # Show command formatting
├── Models/
│   ├── AppSettings.cs                  # Strongly-typed appsettings.json
│   ├── AuthenticationConfig.cs         # Authentication section
│   ├── KestrelConfig.cs                # Kestrel section
│   ├── OrchestrationConfig.cs          # AgenticOrchestration section
│   └── ValidationResult.cs             # Validation result model
├── Hazina.Demo.AgenticOrchestration.ConfigTool.csproj
└── README.md

Hazina.Demo.AgenticOrchestration.ConfigTool.Tests/
├── Commands/
│   ├── SetAuthCommandTests.cs
│   ├── SetKestrelCommandTests.cs
│   └── ...
├── Services/
│   ├── ConfigurationServiceTests.cs
│   ├── ValidationServiceTests.cs
│   └── ...
└── Hazina.Demo.AgenticOrchestration.ConfigTool.Tests.csproj
```

---

## Command-Line Interface

### Global Options

```
--config <path>         Path to appsettings.json (default: .\appsettings.json)
--production            Edit appsettings.Production.json instead of appsettings.json
--verbose, -v           Show detailed output
--silent, -s            Suppress all output except errors
--no-backup             Skip automatic backup creation
--dry-run               Show what would change without modifying files
--help, -h              Show help
--version               Show version
```

### Commands

#### 1. set-auth - Configure Authentication

```bash
HazinaConfigTool set-auth --username <user> --password <pass> [options]

Options:
  --username <user>     Username for web UI login (required)
  --password <pass>     Password for web UI login (required)
  --realm <realm>       Authentication realm (default: "Hazina Agentic Orchestration")
  --enabled <true|false>  Enable/disable authentication (default: true)

Examples:
  HazinaConfigTool set-auth --username admin --password MySecurePass123
  HazinaConfigTool set-auth --username admin --password MyPass --production
  HazinaConfigTool set-auth --enabled false
```

**Exit Codes:**
- 0: Success
- 1: Validation error (empty username/password, invalid realm)
- 2: File access error

#### 2. set-kestrel - Configure Web Server

```bash
HazinaConfigTool set-kestrel --protocol <http|https> --port <port> [options]

Options:
  --protocol <http|https>  HTTP or HTTPS (required)
  --port <port>            Port number (default: 5123)
  --cert <path>            TLS certificate path (required for https)
  --key <path>             TLS private key path (required for https)
  --remove-cert            Remove existing certificate configuration (switch to HTTP)

Examples:
  HazinaConfigTool set-kestrel --protocol https --port 5123 --cert tailscale.crt --key tailscale.key
  HazinaConfigTool set-kestrel --protocol http --port 8080
  HazinaConfigTool set-kestrel --remove-cert
```

**Validation:**
- Port must be 1-65535
- HTTPS requires both --cert and --key
- Certificate and key files must exist and be readable

**Exit Codes:**
- 0: Success
- 1: Validation error (invalid port, missing cert files for HTTPS)
- 2: File access error

#### 3. set-paths - Configure File Paths

```bash
HazinaConfigTool set-paths [options]

Options:
  --database <path>       Database path (default: data\agent-activity.db)
  --logs <path>           Logs directory (default: logs)
  --uploads <path>        Uploads directory (default: uploads)
  --create-dirs           Create directories if they don't exist
  --absolute              Convert relative paths to absolute

Examples:
  HazinaConfigTool set-paths --database "C:\HazinaData\db.sqlite" --logs "C:\HazinaData\logs"
  HazinaConfigTool set-paths --database "data\mydb.db" --create-dirs
  HazinaConfigTool set-paths --logs "D:\Logs\Hazina" --absolute
```

**Validation:**
- Paths must be valid (no invalid characters)
- If --create-dirs: Create directories
- If not --create-dirs: Warn if directories don't exist

**Exit Codes:**
- 0: Success
- 1: Validation error (invalid path characters)
- 2: Directory creation error

#### 4. set-terminal - Configure Terminal Settings

```bash
HazinaConfigTool set-terminal [options]

Options:
  --command <cmd>         Terminal command (default: "claude")
  --workdir <path>        Working directory (default: empty)
  --columns <num>         Terminal columns (default: 120)
  --rows <num>            Terminal rows (default: 30)
  --max-sessions <num>    Max concurrent sessions (default: 10)
  --timeout <minutes>     Session timeout in minutes (default: 60)

Examples:
  HazinaConfigTool set-terminal --command "C:\scripts\claude_agent.bat" --workdir "C:\scripts"
  HazinaConfigTool set-terminal --command "claude" --columns 160 --rows 40
  HazinaConfigTool set-terminal --max-sessions 20 --timeout 120
```

**Validation:**
- Command must not be empty
- Columns/rows must be > 0
- Max sessions must be > 0
- Timeout must be > 0

**Exit Codes:**
- 0: Success
- 1: Validation error
- 2: File access error

#### 5. set-openai - Configure OpenAI Settings

```bash
HazinaConfigTool set-openai [options]

Options:
  --apikey <key>          OpenAI API key
  --model <model>         Chat model (default: gpt-4o-mini)
  --embedding-model <model>  Embedding model (default: text-embedding-3-small)
  --image-model <model>   Image model (default: dall-e-3)
  --tts-model <model>     TTS model (default: gpt-4o-mini-tts)
  --clear-apikey          Remove API key from configuration

Examples:
  HazinaConfigTool set-openai --apikey sk-proj-abc123...
  HazinaConfigTool set-openai --model gpt-4o --embedding-model text-embedding-3-large
  HazinaConfigTool set-openai --clear-apikey
```

**Security:**
- API key stored in appsettings.json (production deployments should use environment variables or secrets manager)
- Show command masks API key (sk-proj-abc***def)

**Exit Codes:**
- 0: Success
- 1: Validation error (invalid API key format)
- 2: File access error

#### 6. validate - Validate Configuration

```bash
HazinaConfigTool validate [options]

Options:
  --strict                Strict validation (fail on warnings)
  --fix                   Attempt to fix validation errors automatically

Examples:
  HazinaConfigTool validate
  HazinaConfigTool validate --strict
  HazinaConfigTool validate --fix --verbose
```

**Checks:**
- ✅ JSON syntax valid
- ✅ Required sections present (Logging, Authentication, AgenticOrchestration)
- ✅ Port number valid (1-65535)
- ✅ If HTTPS: Certificate and key paths exist
- ✅ Database path directory exists
- ✅ Logs path directory exists
- ⚠️ Warnings: Default passwords, missing OpenAI key, HTTP instead of HTTPS

**Output Format:**
```
Validating: C:\Program Files\Hazina Orchestration\appsettings.json

✅ JSON syntax: Valid
✅ Schema: All required sections present
✅ Authentication: Enabled (username: admin)
⚠️ Authentication: Using default password "changeme" - CHANGE THIS!
✅ Kestrel: HTTPS on port 5123
✅ Certificates: Files exist and readable
✅ Database: Path valid, directory exists
✅ Logs: Path valid, directory exists
⚠️ OpenAI: API key not configured

Summary: 6 checks passed, 0 failed, 2 warnings
Status: VALID (with warnings)
```

**Exit Codes:**
- 0: Configuration valid (no errors)
- 1: Validation failed (errors found)
- 3: JSON parsing error

#### 7. show - Display Current Configuration

```bash
HazinaConfigTool show [options]

Options:
  --section <name>        Show specific section only (auth, kestrel, paths, terminal, openai)
  --format <text|json|yaml>  Output format (default: text)
  --show-sensitive        Show sensitive values (passwords, API keys) - USE WITH CAUTION

Examples:
  HazinaConfigTool show
  HazinaConfigTool show --section auth
  HazinaConfigTool show --format json
  HazinaConfigTool show --show-sensitive --verbose
```

**Output Format (text):**
```
Configuration: C:\Program Files\Hazina Orchestration\appsettings.json

╔═══════════════════════════════════════════════════════════════════
║ AUTHENTICATION
╠═══════════════════════════════════════════════════════════════════
║ Enabled:   true
║ Username:  admin
║ Password:  ******** (8 characters)
║ Realm:     Hazina Agentic Orchestration

╔═══════════════════════════════════════════════════════════════════
║ WEB SERVER (KESTREL)
╠═══════════════════════════════════════════════════════════════════
║ Protocol:  HTTPS
║ Port:      5123
║ URL:       https://*:5123
║ Certificate: tailscale.crt
║ Key:       tailscale.key

╔═══════════════════════════════════════════════════════════════════
║ FILE PATHS
╠═══════════════════════════════════════════════════════════════════
║ Database:  C:\scripts\_machine\agent-activity.db
║ Logs:      C:\scripts\logs
║ Uploads:   C:\scripts\uploads

╔═══════════════════════════════════════════════════════════════════
║ TERMINAL
╠═══════════════════════════════════════════════════════════════════
║ Command:   C:\scripts\claude_agent.bat
║ Work Dir:  C:\scripts
║ Columns:   120
║ Rows:      30
║ Max Sessions: 10
║ Timeout:   60 minutes

╔═══════════════════════════════════════════════════════════════════
║ OPENAI
╠═══════════════════════════════════════════════════════════════════
║ API Key:   sk-proj-***...*** (configured)
║ Model:     gpt-4o-mini
║ Embedding: text-embedding-3-small
║ Image:     dall-e-3
║ TTS:       gpt-4o-mini-tts
```

**Exit Codes:**
- 0: Success
- 2: File access error
- 3: JSON parsing error

#### 8. backup - Create Configuration Backup

```bash
HazinaConfigTool backup [options]

Options:
  --output <path>         Backup file path (default: appsettings.json.backup-YYYYMMDD-HHmmss)
  --compress              Create compressed backup (.zip)

Examples:
  HazinaConfigTool backup
  HazinaConfigTool backup --output "C:\Backups\hazina-config-20260301.json"
  HazinaConfigTool backup --compress --output "C:\Backups\hazina-config.zip"
```

**Exit Codes:**
- 0: Success
- 2: File access error

#### 9. restore - Restore Configuration from Backup

```bash
HazinaConfigTool restore --backup <file> [options]

Options:
  --backup <file>         Backup file to restore (required)
  --force                 Skip confirmation prompt

Examples:
  HazinaConfigTool restore --backup appsettings.json.backup-20260301-120000
  HazinaConfigTool restore --backup "C:\Backups\hazina-config.json" --force
```

**Safety:**
- Always creates backup of current config before restoring
- Validates backup file before restoring
- Requires confirmation unless --force

**Exit Codes:**
- 0: Success
- 1: Validation error (invalid backup file)
- 2: File access error
- 4: User cancelled

---

## Configuration File Format

### appsettings.json (Main Configuration)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://*:5123",
        "Certificate": {
          "Path": "tailscale.crt",
          "KeyPath": "tailscale.key"
        }
      }
    }
  },
  "Authentication": {
    "Enabled": true,
    "Username": "admin",
    "Password": "changeme",
    "Realm": "Hazina Agentic Orchestration"
  },
  "AgenticOrchestration": {
    "DatabasePath": "C:\\scripts\\_machine\\agent-activity.db",
    "LogsPath": "C:\\scripts\\logs",
    "EntitiesYamlPath": "entities.yaml",
    "SignalR": { "Enabled": true, "HubPath": "/hubs/agentic" },
    "Polling": {
      "InstanceHeartbeatTimeoutSeconds": 60,
      "InteractionExpiryMinutes": 60
    },
    "Features": {
      "EnableTaskQueue": true,
      "EnableOutputStreaming": true,
      "EnableRealtimeNotifications": true
    },
    "Terminal": {
      "DefaultCommand": "C:\\scripts\\claude_agent.bat",
      "DefaultWorkingDirectory": "C:\\scripts",
      "DefaultArguments": [],
      "DefaultColumns": 120,
      "DefaultRows": 30,
      "MaxConcurrentSessions": 10,
      "SessionTimeoutMinutes": 60
    },
    "SessionLogging": {
      "Enabled": true,
      "BasePath": "C:\\scripts\\logs\\agent-sessions"
    },
    "Uploads": {
      "Path": "C:\\scripts\\uploads",
      "MaxFileSizeMB": 50
    }
  },
  "Swagger": {
    "Enabled": true,
    "Title": "Hazina Agentic Orchestration API",
    "Description": "Web API for managing Claude Code CLI instances",
    "Version": "v1"
  },
  "OpenAI": {
    "ApiKey": "",
    "Model": "gpt-4o-mini",
    "EmbeddingModel": "text-embedding-3-small",
    "ImageModel": "dall-e-3",
    "TtsModel": "gpt-4o-mini-tts"
  }
}
```

### appsettings.Production.json (Production Overrides)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    },
    "EventLog": {
      "LogLevel": {
        "Default": "Information"
      }
    }
  },
  "AllowedHosts": "*",
  "Swagger": {
    "Enabled": true
  },
  "OpenAI": {
    "ApiKey": "",
    "Model": "gpt-4o-mini",
    "EmbeddingModel": "text-embedding-3-small",
    "ImageModel": "dall-e-3",
    "TtsModel": "gpt-4o-mini-tts"
  }
}
```

**Note:** Production config does NOT include Kestrel, Authentication, or Terminal settings. These are environment-specific and configured in main appsettings.json.

---

## Configuration Merging Strategy

When updating configuration:

1. **Read existing configuration** (preserve all values)
2. **Update only specified values** (don't touch unrelated sections)
3. **Validate merged configuration** (ensure consistency)
4. **Create backup** (unless --no-backup)
5. **Write atomically** (temp file + rename to prevent corruption)
6. **Rollback on error** (restore from backup)

**Example:**

```bash
# Current config has HTTPS on port 5123
HazinaConfigTool set-auth --username newuser --password newpass

# After command:
# - Authentication updated (username, password)
# - Kestrel unchanged (still HTTPS, port 5123)
# - All other sections unchanged
```

---

## Error Handling

### Exit Codes

| Code | Meaning | Examples |
|------|---------|----------|
| 0 | Success | Configuration updated successfully |
| 1 | Validation error | Invalid port number, empty username, missing cert file |
| 2 | File access error | Cannot read appsettings.json, permission denied |
| 3 | JSON parsing error | Malformed JSON, syntax error |
| 4 | User cancelled | Restore command cancelled by user |

### Error Messages

**Format:**
```
ERROR: <Short description>
Details: <Longer explanation>
Suggestion: <What to do>

Exit code: <code>
```

**Example:**
```
ERROR: Certificate file not found
Details: The certificate file 'tailscale.crt' does not exist at the specified path.
Suggestion: Generate certificate using 'tailscale cert' or provide correct path.

Exit code: 1
```

### Logging

- **Console:** User-friendly messages (errors, warnings, success)
- **Verbose Mode:** Detailed operation log (file paths, JSON content, validation steps)
- **Silent Mode:** Only errors (for automation)
- **Log File:** Optional (--log-file parameter) for debugging

---

## Testing Strategy

### Unit Tests

- ✅ ConfigurationService: Read, write, merge, validate
- ✅ ValidationService: All validation rules
- ✅ BackupService: Create, restore, verify
- ✅ Each command: Argument parsing, execution, error handling

### Integration Tests

- ✅ End-to-end command execution
- ✅ File system operations (read, write, backup)
- ✅ JSON serialization/deserialization
- ✅ Error scenarios (missing files, invalid JSON, permission errors)

### Manual Tests

- ✅ MSI integration (call from WiX custom action)
- ✅ Setup.ps1 integration (replace Write-AppSettings)
- ✅ Interactive usage (real-world scenarios)
- ✅ Silent mode (automation scenarios)

---

## MSI Integration

### Call from WiX Custom Action

```xml
<CustomAction Id="ConfigureAuth"
              Directory="INSTALLFOLDER"
              ExeCommand='HazinaConfigTool.exe set-auth --username "[AUTH_USERNAME]" --password "[AUTH_PASSWORD]" --silent'
              Execute="deferred"
              Impersonate="no"
              Return="ignore" />

<InstallExecuteSequence>
  <Custom Action="ConfigureAuth" After="InstallFiles">
    NOT Installed AND AUTH_USERNAME AND AUTH_PASSWORD
  </Custom>
</InstallExecuteSequence>
```

**Key Points:**
- `Execute="deferred"` - Runs during commit phase
- `Return="ignore"` - Don't fail MSI if config fails
- `--silent` - No console output
- Properties passed from CredentialsDialog (AUTH_USERNAME, AUTH_PASSWORD)

### Call from Setup.ps1

**Before (inline logic):**
```powershell
Write-AppSettings `
    -TargetDir $InstallDir `
    -Protocol $protocol `
    -Port $Port `
    -CertFile $certPath `
    -KeyFile $keyPath `
    -AuthUser $authUser `
    -AuthPass $authPass `
    ...
```

**After (use HazinaConfigTool):**
```powershell
$configTool = Join-Path $InstallDir "HazinaConfigTool.exe"

# Set authentication
& $configTool set-auth --username $authUser --password $authPass --verbose

# Set Kestrel
if ($protocol -eq "https") {
    & $configTool set-kestrel --protocol https --port $Port --cert $certPath --key $keyPath --verbose
} else {
    & $configTool set-kestrel --protocol http --port $Port --verbose
}

# Set paths
& $configTool set-paths --database $dbPath --logs $logPath --verbose

# Set terminal
& $configTool set-terminal --command $termCmd --workdir $termWorkDir --verbose

# Validate
& $configTool validate --verbose
if ($LASTEXITCODE -ne 0) {
    Write-Host "Configuration validation failed" -ForegroundColor Red
    exit 1
}
```

---

## Build and Deployment

### Build Configuration

**Hazina.Demo.AgenticOrchestration.ConfigTool.csproj:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>13.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- Single-file publish -->
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>false</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>

    <!-- Trimming (reduce size) -->
    <PublishTrimmed>false</PublishTrimmed>

    <!-- Assembly info -->
    <AssemblyName>HazinaConfigTool</AssemblyName>
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
    <FileVersion>1.0.0.0</FileVersion>
    <InformationalVersion>1.0.0</InformationalVersion>

    <!-- Icon -->
    <ApplicationIcon>hazina-icon.ico</ApplicationIcon>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.0" />
  </ItemGroup>
</Project>
```

### Build Commands

```bash
# Development build
dotnet build

# Release build (single-file exe)
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true

# Output: bin\Release\net9.0\win-x64\publish\HazinaConfigTool.exe
```

### Include in MSI

Add to Product-Generated.wxs:
```xml
<Component Id="ConfigTool" Directory="INSTALLFOLDER" Guid="*">
  <File Id="HazinaConfigToolExe"
        Source="$(var.PublishDir)\HazinaConfigTool.exe"
        KeyPath="yes" />
</Component>
```

---

## Documentation

### Help Text

```
HazinaConfigTool - Configuration utility for Hazina Agentic Orchestration

USAGE:
  HazinaConfigTool <command> [options]

COMMANDS:
  set-auth        Configure authentication (username, password)
  set-kestrel     Configure web server (protocol, port, certificates)
  set-paths       Configure file paths (database, logs, uploads)
  set-terminal    Configure terminal settings (command, working directory)
  set-openai      Configure OpenAI API settings
  validate        Validate current configuration
  show            Display current configuration
  backup          Create backup of configuration
  restore         Restore configuration from backup

GLOBAL OPTIONS:
  --config <path>      Path to appsettings.json (default: .\appsettings.json)
  --production         Edit appsettings.Production.json instead
  --verbose, -v        Show detailed output
  --silent, -s         Suppress all output except errors
  --no-backup          Skip automatic backup creation
  --dry-run            Show what would change without modifying files
  --help, -h           Show help
  --version            Show version

EXAMPLES:
  # Configure authentication
  HazinaConfigTool set-auth --username admin --password MySecurePass123

  # Configure HTTPS
  HazinaConfigTool set-kestrel --protocol https --port 5123 --cert tailscale.crt --key tailscale.key

  # Configure paths
  HazinaConfigTool set-paths --database "C:\Data\db.sqlite" --logs "C:\Logs"

  # Validate configuration
  HazinaConfigTool validate --strict

  # Show current configuration
  HazinaConfigTool show --section auth

  # Create backup
  HazinaConfigTool backup --compress --output "C:\Backups\hazina-config.zip"

For detailed help on a command, use:
  HazinaConfigTool <command> --help

EXIT CODES:
  0 - Success
  1 - Validation error
  2 - File access error
  3 - JSON parsing error
  4 - User cancelled
```

---

## Security Considerations

1. **Password Storage:**
   - Passwords stored in plain text in appsettings.json
   - Production deployments should use:
     * Windows Credential Manager
     * Azure Key Vault
     * Environment variables
     * Secrets management system

2. **API Key Storage:**
   - OpenAI API key stored in plain text
   - Same recommendations as passwords

3. **File Permissions:**
   - appsettings.json should be readable only by application user
   - ConfigTool should preserve file permissions when updating

4. **Backup Security:**
   - Backups contain sensitive data (passwords, API keys)
   - Store backups securely
   - Consider encrypting backups (future enhancement)

5. **Logging:**
   - Never log passwords or API keys
   - Use masking in show command (default)
   - Warn when --show-sensitive is used

---

## Future Enhancements

### Version 1.1
- [ ] Interactive mode (prompt for values if not provided)
- [ ] Import/export configuration (JSON, YAML, TOML)
- [ ] Environment variable substitution
- [ ] Configuration profiles (dev, staging, prod)

### Version 1.2
- [ ] GUI wrapper (simple WinForms/WPF app)
- [ ] Encrypted backup support
- [ ] Configuration diff (compare two configs)
- [ ] Migration tool (upgrade config schema)

### Version 2.0
- [ ] Remote configuration (API endpoint)
- [ ] Configuration validation rules engine (custom rules)
- [ ] Integration with Windows Credential Manager
- [ ] Azure Key Vault support

---

## Implementation Checklist

### Phase 1: Core Infrastructure
- [ ] Create project: Hazina.Demo.AgenticOrchestration.ConfigTool
- [ ] Add System.CommandLine package
- [ ] Create Models (AppSettings, AuthenticationConfig, etc.)
- [ ] Create ConfigurationService (read, write, merge)
- [ ] Create ValidationService (basic validation)
- [ ] Create BackupService (backup, restore)

### Phase 2: Commands
- [ ] Implement set-auth command
- [ ] Implement set-kestrel command
- [ ] Implement set-paths command
- [ ] Implement set-terminal command
- [ ] Implement set-openai command
- [ ] Implement validate command
- [ ] Implement show command
- [ ] Implement backup command
- [ ] Implement restore command

### Phase 3: Testing
- [ ] Unit tests for ConfigurationService
- [ ] Unit tests for ValidationService
- [ ] Unit tests for each command
- [ ] Integration tests (end-to-end)
- [ ] Manual testing (real appsettings.json)

### Phase 4: Documentation
- [ ] README.md with usage examples
- [ ] Command help text
- [ ] Error message catalog
- [ ] Troubleshooting guide

### Phase 5: Integration
- [ ] Include in MSI build
- [ ] Update Setup.ps1 to use ConfigTool
- [ ] Update CredentialsDialog to pass values to ConfigTool
- [ ] Test MSI installation with ConfigTool

---

## Success Criteria

✅ **Functional:**
- All commands work as specified
- Configuration merging preserves existing values
- Validation catches all error conditions
- Backup/restore works reliably

✅ **Non-Functional:**
- Fast: < 500ms for simple operations
- Small: Single-file exe < 10 MB
- Reliable: 100% success rate on valid inputs
- User-friendly: Clear error messages, helpful defaults

✅ **Integration:**
- Works from MSI custom action
- Works from Setup.ps1
- Works manually from command line
- Works in automation scripts

✅ **Quality:**
- > 80% code coverage
- All unit tests pass
- All integration tests pass
- No critical bugs

---

## Conclusion

HazinaConfigTool replaces complex inline PowerShell configuration logic with a dedicated, testable, reusable tool. This separation of concerns:

1. **Simplifies MSI installer** - MSI only installs files, ConfigTool handles configuration
2. **Enables automation** - Scriptable CLI for CI/CD and infrastructure as code
3. **Improves reliability** - Single-purpose tool, well-tested, fail-safe operations
4. **Enhances user experience** - Clear commands, helpful errors, validation before changes

Next step: **Task #3** - Implement HazinaConfigTool.exe according to this design.
