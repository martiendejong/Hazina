
$root = "C:\projects\Hazina"
$allProjects = Get-ChildItem -Path $root -Recurse -Filter *.csproj

# Create a map of FileName -> FullPath
$projectMap = @{}
foreach ($proj in $allProjects) {
    if ($projectMap.ContainsKey($proj.Name)) {
        Write-Warning "Duplicate project name found: $($proj.Name). Keeping first encounter: $($projectMap[$proj.Name])"
    }
    else {
        $projectMap[$proj.Name] = $proj.FullName
    }
}

# Function to get relative path
function Get-RelativePath {
    param (
        [string]$From,
        [string]$To
    )
    $fromUri = [Uri]$From
    $toUri = [Uri]$To
    return $fromUri.MakeRelativeUri($toUri).ToString().Replace('/', '\')
}

foreach ($proj in $allProjects) {
    $arg = $proj.FullName
    [xml]$xml = Get-Content $arg
    $ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $ns.AddNamespace("ns", $xml.Project.xmlns)
    
    $changed = $false
    $projectDir = $proj.DirectoryName

    $refs = $xml.SelectNodes("//ns:ProjectReference", $ns)
    if ($refs) {
        foreach ($ref in $refs) {
            $inc = $ref.Include
            
            # Skip if empty
            if ([string]::IsNullOrWhiteSpace($inc)) { continue }

            $exists = $false
            try {
                $chkPath = $inc.Replace('\.', '.') # fast attempt to clean simple escapes
                $fullPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($projectDir, $chkPath))
                $exists = Test-Path $fullPath
            } catch {
                $exists = $false
            }
            
            if (-not $exists) {
                # Clean the include path by removing all backslashes
                $cleanInc = $inc.Replace('\', '')
                
                # Find matching project from map
                $bestMatch = $null
                $maxLen = 0

                foreach ($key in $projectMap.Keys) {
                    if ($cleanInc.EndsWith($key, [System.StringComparison]::OrdinalIgnoreCase)) {
                        if ($key.Length -gt $maxLen) {
                            $maxLen = $key.Length
                            $bestMatch = $key
                        }
                    }
                }

                if ($bestMatch) {
                    $correctPath = $projectMap[$bestMatch]
                    
                    # Calculate new relative path
                    $relUrl = Get-RelativePath -From ($projectDir + "\") -To $correctPath
                    $relPath = [System.Uri]::UnescapeDataString($relUrl)
                    $relPath = $relUrl.Replace('%20', ' ')

                    Write-Host "Fixing reference in $($proj.Name): '$inc' -> '$relPath'"
                    $ref.Include = $relPath
                    $changed = $true
                }
                else {
                    Write-Warning "Could not identify project for reference '$inc' in '$($proj.Name)'"
                }
            }
        }
    }

    if ($changed) {
        $xml.Save($proj.FullName)
    }
}
