# Hazina Configuration Tool

Command-line utility for configuring Hazina Agentic Orchestration.

## Quick Start

```bash
# Configure authentication
HazinaConfigTool set-auth --username admin --password MySecurePass123

# Configure HTTPS
HazinaConfigTool set-kestrel --protocol https --port 5123 --cert tailscale.crt --key tailscale.key

# Validate configuration
HazinaConfigTool validate

# Show current configuration
HazinaConfigTool show
```

## Building

```bash
# Development build
dotnet build

# Release build (single-file executable)
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true

# Output: bin\Release\net9.0\win-x64\publish\HazinaConfigTool.exe
```

## Commands

- `set-auth` - Configure authentication (username, password)
- `set-kestrel` - Configure web server (protocol, port, certificates)
- `set-paths` - Configure file paths (database, logs, uploads)
- `set-terminal` - Configure terminal settings
- `set-openai` - Configure OpenAI API settings
- `validate` - Validate current configuration
- `show` - Display current configuration

Use `HazinaConfigTool <command> --help` for detailed help on each command.

## Global Options

- `--config <path>` - Path to appsettings.json (default: ./appsettings.json)
- `--verbose, -v` - Show detailed output
- `--silent, -s` - Suppress all output except errors
- `--no-backup` - Skip automatic backup creation

## Exit Codes

- 0 - Success
- 1 - Validation error
- 2 - File access error
- 3 - JSON parsing error

## Examples

```bash
# Configure with full settings
HazinaConfigTool set-auth --username admin --password SecurePass123
HazinaConfigTool set-kestrel --protocol https --port 5123 --cert tailscale.crt --key tailscale.key
HazinaConfigTool set-paths --database "C:\Data\db.sqlite" --logs "C:\Logs"
HazinaConfigTool set-terminal --command "claude" --workdir "C:\scripts"

# Validate and show
HazinaConfigTool validate --verbose
HazinaConfigTool show

# Use in scripts (silent mode)
HazinaConfigTool set-auth --username admin --password $env:ADMIN_PASS --silent
if ($LASTEXITCODE -eq 0) {
    Write-Host "Configuration updated successfully"
}
```
