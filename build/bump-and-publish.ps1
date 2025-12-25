param(
    [ValidateSet("patch", "minor", "major")]
    [string]$Increment = "patch",
    [switch]$NoPublish,
    [string]$ApiKey,
    [string]$Source = "https://api.nuget.org/v3/index.json",
    [switch]$SkipBuild,
    [switch]$Yes,
    [string]$Solution = "Libraries/DevGPT.NuGet.sln"
)

$ErrorActionPreference = "Stop"

function Write-Header($title) {
    Write-Host ""
    Write-Host "========================================"
    Write-Host $title
    Write-Host "========================================"
}

function Get-BaselineVersion {
    # Prefer a known anchor project if present
    $anchor = Join-Path $PSScriptRoot "Libraries/LLMs/Classes/DevGPT.LLMs.Classes.csproj"
    $csprojs = @()
    if (Test-Path $anchor) { $csprojs += Get-Item $anchor }
    $csprojs += Get-ChildItem -Path $PSScriptRoot -Recurse -Filter *.csproj | Where-Object { $_.FullName -ne $anchor }

    foreach ($f in $csprojs) {
        try {
            [xml]$xml = Get-Content -Raw $f.FullName
            $verNode = $xml.SelectSingleNode("//Version")
            if ($verNode -and $verNode.InnerText -match '^(\d+)\.(\d+)\.(\d+)$') {
                return [PSCustomObject]@{ Path = $f.FullName; Version = $verNode.InnerText }
            }
            $pkgVerNode = $xml.SelectSingleNode("//PackageVersion")
            if ($pkgVerNode -and $pkgVerNode.InnerText -match '^(\d+)\.(\d+)\.(\d+)$') {
                return [PSCustomObject]@{ Path = $f.FullName; Version = $pkgVerNode.InnerText }
            }
        } catch { }
    }
    throw "Could not determine a baseline version from any .csproj. Ensure a <Version> or <PackageVersion> element exists."
}

function Bump-Version([string]$version, [string]$type) {
    if ($version -notmatch '^(\d+)\.(\d+)\.(\d+)$') {
        throw "Invalid version format '$version'. Expected MAJOR.MINOR.PATCH"
    }
    $major = [int]$Matches[1]
    $minor = [int]$Matches[2]
    $patch = [int]$Matches[3]
    switch ($type) {
        "patch" { $patch++ }
        "minor" { $minor++; $patch = 0 }
        "major" { $major++; $minor = 0; $patch = 0 }
        default { throw "Unknown increment type: $type" }
    }
    return "$major.$minor.$patch"
}

function Update-AllProjectVersions([string]$targetVersion) {
    Write-Host "Updating all project versions to $targetVersion ..."
    $updated = 0
    $failed = 0
    $csprojFiles = Get-ChildItem -Path $PSScriptRoot -Recurse -Filter *.csproj
    foreach ($path in $csprojFiles) {
        try {
            [xml]$xml = Get-Content -Raw $path.FullName
            $changed = $false
            foreach ($node in $xml.SelectNodes("//Version")) {
                if ($node.InnerText -ne $targetVersion) { $node.InnerText = $targetVersion; $changed = $true }
            }
            foreach ($node in $xml.SelectNodes("//PackageVersion")) {
                if ($node.InnerText -ne $targetVersion) { $node.InnerText = $targetVersion; $changed = $true }
            }
            if (-not $xml.SelectNodes("//Version")) {
                $propGroup = $xml.CreateElement("PropertyGroup")
                $ver = $xml.CreateElement("Version")
                $ver.InnerText = $targetVersion
                $propGroup.AppendChild($ver) | Out-Null
                $xml.Project.AppendChild($propGroup) | Out-Null
                $changed = $true
            }
            if ($changed) { $xml.Save($path.FullName); $updated++ }
        } catch {
            Write-Warning "Failed updating $($path.FullName): $_"
            $failed++
        }
    }
    Write-Host "Updated: $updated; Failed: $failed"
    if ($failed -gt 0) { throw "One or more project files failed to update." }
}

function Build-And-Pack([string]$solutionPath) {
    if (-not (Test-Path $solutionPath)) {
        throw "Solution file '$solutionPath' does not exist."
    }
    Write-Header "Building packages (Release)"
    dotnet clean $solutionPath | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet clean failed" }
    dotnet build $solutionPath -c Release | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }
    Write-Header "Packing packages (Release)"
    dotnet pack $solutionPath -c Release --no-build | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed" }
}

function Publish-Packages([string]$apiKey, [string]$source, [string]$version) {
    if (-not $apiKey) { $apiKey = $env:NUGET_API_KEY }
    if (-not $apiKey) { throw "NuGet API key not provided. Pass -ApiKey or set NUGET_API_KEY." }

    Write-Header "Publishing packages to $source"
    $nupkgs = Get-ChildItem -Path $PSScriptRoot -Recurse -Filter *.nupkg |
        Where-Object {
            $_.FullName -notmatch "\.symbols\.nupkg$" -and
            $_.FullName -match "\\bin\\Release\\" -and
            $_.Name -like "*.$version.nupkg"
        }
    if (-not $nupkgs) {
        throw "No packages found under bin/Release. Did packing succeed?"
    }
    $success = 0
    $fail = 0
    $failedPkgs = @()
    foreach ($pkg in $nupkgs) {
        Write-Host "Pushing $($pkg.FullName) ..."
        $push = Start-Process -FilePath "dotnet" -ArgumentList @("nuget","push","$($pkg.FullName)","--api-key","$apiKey","--source","$source","--skip-duplicate") -Wait -PassThru -NoNewWindow
        if ($push.ExitCode -eq 0) { $success++ } else { $fail++; $failedPkgs += $pkg.FullName }
    }
    Write-Host "Publish successful: $success; failed: $fail"
    if ($fail -gt 0) {
        Write-Warning "Failed packages:"; $failedPkgs | ForEach-Object { Write-Host " - $_" }
        throw "One or more packages failed to publish."
    }
}

Write-Header "DevGPT NuGet Version Manager"
Write-Host "Increment: $Increment"
Write-Host "Publish: $(!$NoPublish)"
Write-Host "Source: $Source"
Write-Host "Solution: $Solution"

$solutionPath = Join-Path $PSScriptRoot $Solution
if (-not (Test-Path $solutionPath)) {
    $fallbackSln = Get-ChildItem -Path $PSScriptRoot -Filter *.sln | Select-Object -First 1
    if ($fallbackSln) {
        Write-Host "Solution '$Solution' not found, using '$($fallbackSln.Name)' instead."
        $solutionPath = $fallbackSln.FullName
    } else {
        throw "No .sln files found in the repository root."
    }
}

$baseline = Get-BaselineVersion
$currentVersion = $baseline.Version
Write-Host "Current version ($($baseline.Path)): $currentVersion"

$newVersion = Bump-Version -version $currentVersion -type $Increment
Write-Host "New version: $newVersion"

if (-not $Yes) {
    $answer = Read-Host "Update all packages to $newVersion and continue? (y/n)"
    if ($answer -ne 'y') { Write-Host "Cancelled."; exit 0 }
}

Update-AllProjectVersions -targetVersion $newVersion

if (-not $SkipBuild) {
    Build-And-Pack -solutionPath $solutionPath
}

if (-not $NoPublish) {
    Publish-Packages -apiKey $ApiKey -source $Source -version $newVersion
}

Write-Header "Done"
Write-Host "Version: $currentVersion => $newVersion"
Write-Host "Next steps:"
Write-Host " - Commit: git add . && git commit -m 'Bump version to $newVersion'"
Write-Host " - Tag:    git tag v$newVersion"
Write-Host " - Push:   git push && git push --tags"
