# Public downloads are deliberately independent of other packaging/CI evidence.
function Get-PublicReleasePayloadNames([string]$Version) {
    if ($Version -notmatch '^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$') { throw 'Invalid release version.' }
    return @("HourstoneCompanion-$Version-full.nupkg", 'HourstoneCompanion-win-Portable.zip',
        'HourstoneCompanion-win-Setup.exe', 'releases.win.json')
}

function Write-PublicReleaseManifest([string]$ReleaseDirectory, [string]$Version, [bool]$Signed) {
    $entries = @(Get-PublicReleasePayloadNames $Version | Sort-Object | ForEach-Object {
        $path = Join-Path $ReleaseDirectory $_
        $file = Get-Item -LiteralPath $path -ErrorAction Stop
        if ($file -isnot [IO.FileInfo] -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Public release assets must be regular files.'
        }
        @{ file = $_; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256 -ErrorAction Stop).Hash.ToLowerInvariant() }
    })
    $newline = [string][char]10
    $checksums = ($entries | ForEach-Object { $_.sha256 + '  ' + $_.file }) -join $newline
    $checksumPath = Join-Path $ReleaseDirectory 'SHA256SUMS'
    [IO.File]::WriteAllText($checksumPath, $checksums + $newline, [Text.UTF8Encoding]::new($false))
    $entries += @{ file = 'SHA256SUMS'; sha256 = (Get-FileHash -LiteralPath $checksumPath -Algorithm SHA256 -ErrorAction Stop).Hash.ToLowerInvariant() }
    $manifest = @{ formatVersion = 1; version = $Version; runtime = 'win-x64'; signed = $Signed; assets = $entries }
    # This manifest is retained locally and in CI, but is never a public download.
    [IO.File]::WriteAllText((Join-Path $ReleaseDirectory 'release-manifest.json'),
        ($manifest | ConvertTo-Json -Depth 5) + $newline, [Text.UTF8Encoding]::new($false))
}

function Assert-PublicReleaseChecksums([string]$ReleaseDirectory, $Entries, [string]$Version) {
    $payloadNames = @(Get-PublicReleasePayloadNames $Version)
    $lines = @(Get-Content -LiteralPath (Join-Path $ReleaseDirectory 'SHA256SUMS') -ErrorAction Stop)
    if ($lines.Count -ne $payloadNames.Count) { throw 'SHA256SUMS must cover exactly the four public payload files.' }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($line in $lines) {
        if ($line -notmatch '^([a-fA-F0-9]{64})  (.+)$') { throw 'Invalid SHA256SUMS entry.' }
        $hash, $name = $Matches[1], $Matches[2]
        if ($name -cnotin $payloadNames -or -not $seen.Add($name)) { throw 'SHA256SUMS contains an unexpected or duplicate file.' }
        $entry = @($Entries | Where-Object { $_.file -ceq $name })
        if ($entry.Count -ne 1 -or $entry[0].sha256 -ne $hash) { throw 'SHA256SUMS does not match the validated public assets.' }
    }
}
