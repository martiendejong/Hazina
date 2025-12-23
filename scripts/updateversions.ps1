# PowerShell script to update Version across csproj files
# Usage: updateversions.ps1 or set $targetVersion variable before calling

# Check if $targetVersion is already set (called from another script)
if (-not $targetVersion) {
    $targetVersion = "1.0.5"  # Default fallback version
}

Write-Host "Updating Version to $targetVersion in all csproj files..."
$csprojFiles = Get-ChildItem -Path . -Recurse -Filter *.csproj
foreach ($path in $csprojFiles) {
    try {
        [xml]$xml = Get-Content $path.FullName
        $changed = $false
        foreach ($node in $xml.SelectNodes("//Version")) {
            if ($node.InnerText -ne $targetVersion) {
                $node.InnerText = $targetVersion
                $changed = $true
            }
        }
        foreach ($node in $xml.SelectNodes("//PackageVersion")) {
            if ($node.InnerText -ne $targetVersion) {
                $node.InnerText = $targetVersion
                $changed = $true
            }
        }
        if (-not $xml.SelectNodes("//Version")) {
            $propGroup = $xml.CreateElement("PropertyGroup")
            $ver = $xml.CreateElement("Version")
            $ver.InnerText = $targetVersion
            $propGroup.AppendChild($ver) | Out-Null
            $xml.Project.AppendChild($propGroup) | Out-Null
            $changed = $true
        }
        if ($changed) {
            $xml.Save($path.FullName)
            Write-Host "Updated $($path.FullName)"
        }
    } catch {
        Write-Warning "Failed to update $($path.FullName): $_"
    }
}
# Optional: Update Directory.Build.props if present
$propsFiles = Get-ChildItem -Path . -Recurse -Filter Directory.Build.props
foreach ($pf in $propsFiles) {
    try {
        [xml]$xmlP = Get-Content $pf.FullName
        $changedP = $false
        foreach ($node in $xmlP.SelectNodes("//Version")) {
            if ($node.InnerText -ne $targetVersion) {
                $node.InnerText = $targetVersion
                $changedP = $true
            }
        }
        if (-not $xmlP.SelectNodes("//Version")) {
            $propGroup = $xmlP.CreateElement("PropertyGroup")
            $ver = $xmlP.CreateElement("Version")
            $ver.InnerText = $targetVersion
            $propGroup.AppendChild($ver) | Out-Null
            $xmlP.Project.AppendChild($propGroup) | Out-Null
            $changedP = $true
        }
        if ($changedP) { $xmlP.Save($pf.FullName) }
    } catch {
        Write-Warning "Failed to update $($pf.FullName): $_"
    }
}
Write-Host "Version update complete."