using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace Hazina.App.HazinaCoder.Core.Tools;

/// <summary>
/// Integrates with package managers (NuGet, npm, pip)
/// </summary>
public class PackageManagerTool
{
    private readonly string _workingDirectory;

    public PackageManagerTool(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
    }

    /// <summary>
    /// Detect package manager from project files
    /// </summary>
    public PackageManager DetectPackageManager()
    {
        // Check for .csproj files (NuGet)
        if (Directory.GetFiles(_workingDirectory, "*.csproj", SearchOption.TopDirectoryOnly).Any())
            return PackageManager.NuGet;

        // Check for package.json (npm)
        if (File.Exists(Path.Combine(_workingDirectory, "package.json")))
            return PackageManager.Npm;

        // Check for requirements.txt or pyproject.toml (pip)
        if (File.Exists(Path.Combine(_workingDirectory, "requirements.txt")) ||
            File.Exists(Path.Combine(_workingDirectory, "pyproject.toml")))
            return PackageManager.Pip;

        // Check for Cargo.toml (Cargo)
        if (File.Exists(Path.Combine(_workingDirectory, "Cargo.toml")))
            return PackageManager.Cargo;

        // Check for go.mod (Go modules)
        if (File.Exists(Path.Combine(_workingDirectory, "go.mod")))
            return PackageManager.Go;

        return PackageManager.Unknown;
    }

    /// <summary>
    /// Install a package
    /// </summary>
    public async Task<OperationResult> InstallPackageAsync(string packageName, string? version = null, PackageManager? manager = null)
    {
        manager ??= DetectPackageManager();

        var command = manager switch
        {
            PackageManager.NuGet => version != null
                ? $"dotnet add package {packageName} --version {version}"
                : $"dotnet add package {packageName}",
            PackageManager.Npm => version != null
                ? $"npm install {packageName}@{version}"
                : $"npm install {packageName}",
            PackageManager.Pip => version != null
                ? $"pip install {packageName}=={version}"
                : $"pip install {packageName}",
            PackageManager.Cargo => version != null
                ? $"cargo add {packageName}@{version}"
                : $"cargo add {packageName}",
            PackageManager.Go => $"go get {packageName}@{version ?? "latest"}",
            _ => throw new NotSupportedException($"Package manager {manager} not supported")
        };

        return await ExecuteCommandAsync(command, manager.ToString()!);
    }

    /// <summary>
    /// Update a package
    /// </summary>
    public async Task<OperationResult> UpdatePackageAsync(string packageName, PackageManager? manager = null)
    {
        manager ??= DetectPackageManager();

        var command = manager switch
        {
            PackageManager.NuGet => $"dotnet add package {packageName}",
            PackageManager.Npm => $"npm update {packageName}",
            PackageManager.Pip => $"pip install --upgrade {packageName}",
            PackageManager.Cargo => $"cargo update {packageName}",
            PackageManager.Go => $"go get -u {packageName}",
            _ => throw new NotSupportedException($"Package manager {manager} not supported")
        };

        return await ExecuteCommandAsync(command, manager.ToString()!);
    }

    /// <summary>
    /// List outdated packages
    /// </summary>
    public async Task<OutdatedPackagesResult> ListOutdatedAsync(PackageManager? manager = null)
    {
        manager ??= DetectPackageManager();

        var command = manager switch
        {
            PackageManager.NuGet => "dotnet list package --outdated",
            PackageManager.Npm => "npm outdated --json",
            PackageManager.Pip => "pip list --outdated --format=json",
            PackageManager.Cargo => "cargo outdated --format json",
            PackageManager.Go => "go list -u -m -json all",
            _ => throw new NotSupportedException($"Package manager {manager} not supported")
        };

        var result = await ExecuteCommandAsync(command, manager.ToString()!);

        if (!result.Success)
        {
            return new OutdatedPackagesResult(Success: false, Packages: Array.Empty<OutdatedPackage>(), ErrorMessage: result.ErrorMessage);
        }

        var packages = ParseOutdatedPackages(result.Output, manager.Value);
        return new OutdatedPackagesResult(Success: true, Packages: packages, ErrorMessage: null);
    }

    /// <summary>
    /// Audit for security vulnerabilities
    /// </summary>
    public async Task<SecurityAuditResult> AuditSecurityAsync(PackageManager? manager = null)
    {
        manager ??= DetectPackageManager();

        var command = manager switch
        {
            PackageManager.NuGet => "dotnet list package --vulnerable --include-transitive",
            PackageManager.Npm => "npm audit --json",
            PackageManager.Pip => "pip-audit --format json",
            PackageManager.Cargo => "cargo audit --json",
            _ => throw new NotSupportedException($"Security audit not available for {manager}")
        };

        var result = await ExecuteCommandAsync(command, manager.ToString()!, throwOnError: false);

        // npm audit returns exit code 1 if vulnerabilities found
        if (!result.Success && !result.Output.Contains("vulnerabilities"))
        {
            return new SecurityAuditResult(Success: false, Vulnerabilities: Array.Empty<Vulnerability>(), Summary: null, ErrorMessage: result.ErrorMessage);
        }

        var vulns = ParseVulnerabilities(result.Output, manager.Value);
        var summary = $"Found {vulns.Length} vulnerabilities";

        return new SecurityAuditResult(Success: true, Vulnerabilities: vulns, Summary: summary, ErrorMessage: null);
    }

    /// <summary>
    /// Search for packages
    /// </summary>
    public async Task<SearchResult> SearchPackagesAsync(string query, PackageManager? manager = null)
    {
        manager ??= DetectPackageManager();

        var command = manager switch
        {
            PackageManager.NuGet => $"dotnet package search {query}",
            PackageManager.Npm => $"npm search {query} --json",
            PackageManager.Pip => $"pip search {query}", // Note: PyPI search may be disabled
            PackageManager.Cargo => $"cargo search {query}",
            _ => throw new NotSupportedException($"Search not available for {manager}")
        };

        var result = await ExecuteCommandAsync(command, manager.ToString()!, throwOnError: false);

        if (!result.Success)
        {
            return new SearchResult(Success: false, Packages: Array.Empty<PackageSearchResult>(), ErrorMessage: result.ErrorMessage);
        }

        var packages = ParseSearchResults(result.Output, manager.Value);
        return new SearchResult(Success: true, Packages: packages, ErrorMessage: null);
    }

    /// <summary>
    /// Display outdated packages in console
    /// </summary>
    public void DisplayOutdatedPackages(OutdatedPackagesResult result)
    {
        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]Error: {result.ErrorMessage}[/]");
            return;
        }

        if (!result.Packages.Any())
        {
            AnsiConsole.MarkupLine("[green]✓ All packages are up to date[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Package")
            .AddColumn("Current")
            .AddColumn("Latest")
            .AddColumn("Type");

        foreach (var pkg in result.Packages)
        {
            var typeColor = pkg.UpdateType switch
            {
                UpdateType.Major => "red",
                UpdateType.Minor => "yellow",
                UpdateType.Patch => "green",
                _ => "white"
            };

            table.AddRow(
                pkg.Name,
                pkg.CurrentVersion,
                pkg.LatestVersion,
                $"[{typeColor}]{pkg.UpdateType}[/]"
            );
        }

        AnsiConsole.MarkupLine($"[yellow]Found {result.Packages.Length} outdated package(s)[/]");
        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Display security audit results
    /// </summary>
    public void DisplaySecurityAudit(SecurityAuditResult result)
    {
        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]Error: {result.ErrorMessage}[/]");
            return;
        }

        if (!result.Vulnerabilities.Any())
        {
            AnsiConsole.MarkupLine("[green]✓ No known vulnerabilities detected[/]");
            return;
        }

        var criticalCount = result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Critical);
        var highCount = result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.High);

        AnsiConsole.MarkupLine($"[red]⚠ {result.Vulnerabilities.Length} vulnerabilities found[/]");
        AnsiConsole.MarkupLine($"  Critical: [red]{criticalCount}[/]  High: [yellow]{highCount}[/]");
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Package")
            .AddColumn("Severity")
            .AddColumn("Description")
            .AddColumn("Fixed Version");

        foreach (var vuln in result.Vulnerabilities.OrderByDescending(v => v.Severity))
        {
            var severityColor = vuln.Severity switch
            {
                VulnerabilitySeverity.Critical => "red",
                VulnerabilitySeverity.High => "yellow",
                VulnerabilitySeverity.Medium => "orange1",
                _ => "blue"
            };

            table.AddRow(
                vuln.PackageName,
                $"[{severityColor}]{vuln.Severity}[/]",
                $"[dim]{vuln.Title[..Math.Min(40, vuln.Title.Length)]}...[/]",
                vuln.FixedVersion ?? "N/A"
            );
        }

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Execute shell command
    /// </summary>
    private async Task<OperationResult> ExecuteCommandAsync(string command, string context, bool throwOnError = false)
    {
        try
        {
            var parts = command.Split(' ', 2);
            var executable = parts[0];
            var arguments = parts.Length > 1 ? parts[1] : "";

            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = _workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return new OperationResult(false, "", $"Failed to start process: {command}");
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var success = process.ExitCode == 0 || !throwOnError;
            var errorMessage = !success ? error : null;

            return new OperationResult(success, output, errorMessage);
        }
        catch (Exception ex)
        {
            return new OperationResult(false, "", ex.Message);
        }
    }

    /// <summary>
    /// Parse outdated packages from output
    /// </summary>
    private OutdatedPackage[] ParseOutdatedPackages(string output, PackageManager manager)
    {
        var packages = new List<OutdatedPackage>();

        try
        {
            if (manager == PackageManager.Npm && output.StartsWith("{"))
            {
                var json = JsonDocument.Parse(output);
                foreach (var prop in json.RootElement.EnumerateObject())
                {
                    var pkg = prop.Value;
                    packages.Add(new OutdatedPackage(
                        Name: prop.Name,
                        CurrentVersion: pkg.GetProperty("current").GetString() ?? "",
                        LatestVersion: pkg.GetProperty("latest").GetString() ?? "",
                        UpdateType: DetermineUpdateType(
                            pkg.GetProperty("current").GetString() ?? "",
                            pkg.GetProperty("latest").GetString() ?? ""
                        )
                    ));
                }
            }
            else
            {
                // Parse line-based output
                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    // Simple regex to extract package info
                    var match = Regex.Match(line, @"(\S+)\s+(\S+)\s+(\S+)");
                    if (match.Success)
                    {
                        packages.Add(new OutdatedPackage(
                            Name: match.Groups[1].Value,
                            CurrentVersion: match.Groups[2].Value,
                            LatestVersion: match.Groups[3].Value,
                            UpdateType: DetermineUpdateType(match.Groups[2].Value, match.Groups[3].Value)
                        ));
                    }
                }
            }
        }
        catch
        {
            // Parsing failed, return empty
        }

        return packages.ToArray();
    }

    /// <summary>
    /// Parse vulnerabilities from audit output
    /// </summary>
    private Vulnerability[] ParseVulnerabilities(string output, PackageManager manager)
    {
        var vulns = new List<Vulnerability>();

        try
        {
            if (output.StartsWith("{"))
            {
                var json = JsonDocument.Parse(output);
                // npm audit format
                if (json.RootElement.TryGetProperty("vulnerabilities", out var vulnsProperty))
                {
                    foreach (var prop in vulnsProperty.EnumerateObject())
                    {
                        var vuln = prop.Value;
                        vulns.Add(new Vulnerability(
                            PackageName: prop.Name,
                            Severity: ParseSeverity(vuln.GetProperty("severity").GetString() ?? ""),
                            Title: vuln.GetProperty("title").GetString() ?? "Unknown vulnerability",
                            FixedVersion: vuln.TryGetProperty("fixAvailable", out var fix) ? fix.GetString() : null
                        ));
                    }
                }
            }
        }
        catch
        {
            // Parsing failed
        }

        return vulns.ToArray();
    }

    /// <summary>
    /// Parse search results
    /// </summary>
    private PackageSearchResult[] ParseSearchResults(string output, PackageManager manager)
    {
        // Simplified implementation
        return Array.Empty<PackageSearchResult>();
    }

    /// <summary>
    /// Determine update type (major/minor/patch)
    /// </summary>
    private UpdateType DetermineUpdateType(string current, string latest)
    {
        var currentParts = current.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        var latestParts = latest.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();

        if (currentParts.Length >= 1 && latestParts.Length >= 1 && currentParts[0] < latestParts[0])
            return UpdateType.Major;
        if (currentParts.Length >= 2 && latestParts.Length >= 2 && currentParts[1] < latestParts[1])
            return UpdateType.Minor;
        return UpdateType.Patch;
    }

    /// <summary>
    /// Parse severity string
    /// </summary>
    private VulnerabilitySeverity ParseSeverity(string severity)
    {
        return severity.ToLower() switch
        {
            "critical" => VulnerabilitySeverity.Critical,
            "high" => VulnerabilitySeverity.High,
            "moderate" or "medium" => VulnerabilitySeverity.Medium,
            _ => VulnerabilitySeverity.Low
        };
    }
}

// ===== Data Models =====

public enum PackageManager
{
    Unknown,
    NuGet,
    Npm,
    Pip,
    Cargo,
    Go
}

public enum UpdateType
{
    Patch,
    Minor,
    Major
}

public enum VulnerabilitySeverity
{
    Low,
    Medium,
    High,
    Critical
}

public record OperationResult(bool Success, string Output, string? ErrorMessage);

public record OutdatedPackage(string Name, string CurrentVersion, string LatestVersion, UpdateType UpdateType);

public record OutdatedPackagesResult(bool Success, OutdatedPackage[] Packages, string? ErrorMessage);

public record Vulnerability(string PackageName, VulnerabilitySeverity Severity, string Title, string? FixedVersion);

public record SecurityAuditResult(bool Success, Vulnerability[] Vulnerabilities, string? Summary, string? ErrorMessage);

public record PackageSearchResult(string Name, string Description, string LatestVersion);

public record SearchResult(bool Success, PackageSearchResult[] Packages, string? ErrorMessage);
