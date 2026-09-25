#Requires -Version 7.4
<#
.SYNOPSIS
Preflight an explicit WoW client; replace only its Hourstone addon with -Apply.
.DESCRIPTION
No discovery, SQLite reads, SavedVariables access or game launch. Close WoW and
do not start it during application. A process whose path cannot be read blocks
the operation. Backups remain outside WoW and this repository under LocalAppData.
Each client is a separate transaction; earlier successful clients stay updated
if a later client fails. Dot-source only to load functions for isolated tests.
.EXAMPLE
./tools/update-local-addon.ps1 -ClientDirectory 'D:\Games\World of Warcraft\_retail_'
.EXAMPLE
./tools/update-local-addon.ps1 -ClientDirectory 'D:\Games\World of Warcraft\_retail_' -Apply
#>
[CmdletBinding()]
param(
    [string[]]$ClientDirectory = @(),
    [string]$ArchivePath = (Join-Path $PSScriptRoot '../artifacts/addon/Hourstone-0.3.2.zip'),
    [ValidatePattern('^[0-9a-fA-F]{64}$')][string]$ExpectedSha256 = '362adf37e0969c3267be68e4717ce9e97fd9a01da0c30903084bbe2adca0de5c',
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$ExpectedVersion = '0.3.2',
    [switch]$Apply,
    [switch]$Preflight
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-AddonBackupRoot {
    Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Hourstone/Backups/Addon'
}
function Test-AddonWithin([string]$Path, [string]$Parent) {
    $pathFull = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    $parentFull = [IO.Path]::GetFullPath($Parent).TrimEnd('\', '/')
    $pathFull.Equals($parentFull, [StringComparison]::OrdinalIgnoreCase) -or
        $pathFull.StartsWith($parentFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}
function Assert-AddonNoLinks([string]$Path) {
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        # GetAttributes also detects a dangling link, unlike File/Directory.Exists.
        try { $attributes = [IO.File]::GetAttributes($current) }
        catch [IO.FileNotFoundException] { $attributes = $null }
        catch [IO.DirectoryNotFoundException] { $attributes = $null }
        if ($null -ne $attributes -and ($attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'A target, archive or backup path contains a reparse point; nothing may be followed.'
        }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}
function Get-AddonTree([string]$Directory) {
    Assert-AddonNoLinks $Directory
    $pending = [Collections.Generic.Stack[string]]::new(); $pending.Push($Directory)
    $entries = [Collections.Generic.List[object]]::new()
    while ($pending.Count) {
        $current = $pending.Pop()
        foreach ($item in [IO.DirectoryInfo]::new($current).EnumerateFileSystemInfos()) {
            Assert-AddonNoLinks $item.FullName
            if ($item.Name -ieq 'SavedVariables' -or $item.Name -ieq 'WTF') { throw 'Unexpected game-data directory inside the addon; it will not be read or changed.' }
            if (-not (Test-AddonWithin $item.FullName $Directory)) { throw 'Addon tree leaves its expected directory.' }
            $directoryEntry = ($item.Attributes -band [IO.FileAttributes]::Directory) -ne 0
            $entries.Add([pscustomobject]@{ Path = $item.FullName; Relative = [IO.Path]::GetRelativePath($Directory, $item.FullName); Directory = $directoryEntry })
            if ($entries.Count -gt 4096) { throw 'Existing addon tree exceeds the 4096-entry safety limit.' }
            if ($directoryEntry) { $pending.Push($item.FullName) }
        }
    }
    @($entries | Sort-Object Relative)
}
function Get-AddonTreeFingerprint([string]$Directory) {
    $parts = foreach ($item in @(Get-AddonTree $Directory)) {
        if ($item.Directory) { 'D:' + $item.Relative }
        else { 'F:' + $item.Relative + ':' + (Get-FileHash -LiteralPath $item.Path -Algorithm SHA256).Hash }
    }
    $data = [Text.Encoding]::UTF8.GetBytes(($parts -join "`n"))
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data))
}
function Copy-AddonTree([string]$Source, [string]$Destination) {
    Assert-AddonNoLinks $Source; Assert-AddonNoLinks $Destination
    if ([IO.Directory]::Exists($Destination) -or [IO.File]::Exists($Destination)) { throw 'Staging/backup destination already exists.' }
    $items = @(Get-AddonTree $Source)
    [IO.Directory]::CreateDirectory($Destination) | Out-Null
    foreach ($item in $items) {
        Assert-AddonNoLinks $item.Path
        $target = Join-Path $Destination $item.Relative
        if (-not (Test-AddonWithin $target $Destination)) { throw 'Copy destination leaves its expected directory.' }
        Assert-AddonNoLinks $target
        if ($item.Directory) { [IO.Directory]::CreateDirectory($target) | Out-Null }
        else {
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            [IO.File]::Copy($item.Path, $target, $false)
        }
    }
}
function Remove-AddonOwnedTree([string]$Path, [string]$Parent) {
    $full = [IO.Path]::GetFullPath($Path); $parentFull = [IO.Path]::GetFullPath($Parent)
    if (-not (Test-AddonWithin $full $parentFull) -or $full.TrimEnd('\').Equals($parentFull.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Cleanup target is not a child of the intended staging directory.'
    }
    Assert-AddonNoLinks $full
    if (-not [IO.Directory]::Exists($full)) { return }
    $items = @(Get-AddonTree $full)
    foreach ($item in $items | Where-Object { -not $_.Directory }) { Assert-AddonNoLinks $item.Path; [IO.File]::Delete($item.Path) }
    foreach ($item in $items | Where-Object Directory | Sort-Object { $_.Path.Length } -Descending) { [IO.Directory]::Delete($item.Path, $false) }
    [IO.Directory]::Delete($full, $false)
}
function Assert-AddonClientIdle([string]$Client) {
    try { $processes = @(Get-CimInstance -ClassName Win32_Process -Filter "Name LIKE 'Wow%.exe'" -ErrorAction Stop) }
    catch { throw 'WoW process inspection failed; update is blocked.' }
    foreach ($process in $processes) {
        if ($process.Name -notmatch '(?i)^Wow.*\.exe$') { continue }
        if ([string]::IsNullOrWhiteSpace($process.ExecutablePath) -or -not [IO.Path]::IsPathFullyQualified($process.ExecutablePath)) {
            throw 'A WoW process has an unreadable executable path; update is blocked.'
        }
        if (Test-AddonWithin $process.ExecutablePath $Client) { throw 'WoW is running from this client directory; close it before updating.' }
    }
}
function Get-AddonVersion([string]$Toc) {
    if (-not [IO.File]::Exists($Toc)) { throw 'An existing Hourstone directory has no Hourstone.toc; it is not a recognized addon.' }
    Assert-AddonNoLinks $Toc
    $matches = [regex]::Matches([IO.File]::ReadAllText($Toc), '(?m)^## Version:\s*(\d+\.\d+\.\d+)\s*$')
    if ($matches.Count -ne 1) { throw 'Addon TOC must declare exactly one numeric version.' }
    $matches[0].Groups[1].Value
}
function Get-AddonArchivePlan([IO.Compression.ZipArchive]$Zip, [string]$Version) {
    if ($Zip.Entries.Count -eq 0 -or $Zip.Entries.Count -gt 512) { throw 'Unexpected archive entry count.' }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $files = [Collections.Generic.List[object]]::new(); [long]$total = 0
    foreach ($entry in $Zip.Entries) {
        $name = $entry.FullName; $trimmed = $name.TrimEnd('/')
        if ($name -match '[\\:\x00-\x1f\x7f]' -or $name.StartsWith('/') -or $trimmed -eq '' -or -not $seen.Add($trimmed)) { throw 'Unsafe or duplicate archive path.' }
        $segments = $trimmed.Split('/')
        if ($segments[0] -cne 'Hourstone') { throw 'Archive must contain only the Hourstone addon directory.' }
        foreach ($segment in $segments) {
            if ($segment -in @('', '.', '..') -or $segment -match '[<>"|?*]' -or $segment.EndsWith('.') -or $segment.EndsWith(' ') -or
                $segment -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)' -or $segment -ieq 'SavedVariables' -or $segment -ieq 'WTF') { throw 'Unsafe archive path component.' }
        }
        $unixType = ($entry.ExternalAttributes -shr 16) -band 0xF000
        if ($unixType -notin @(0, 0x8000, 0x4000) -or ($entry.ExternalAttributes -band 0x400)) { throw 'Archive links or special files are not supported.' }
        if ($name.EndsWith('/')) { if ($entry.Length -ne 0) { throw 'Invalid archive directory.' }; continue }
        if ($segments.Count -lt 2 -or $unixType -eq 0x4000) { throw 'Invalid archive file.' }
        $total += $entry.Length
        if ($entry.Length -gt 16MB -or $total -gt 32MB) { throw 'Archive exceeds the extraction safety limit.' }
        $inputStream = $entry.Open()
        try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($inputStream)) }
        finally { $inputStream.Dispose() }
        $files.Add([pscustomobject]@{ Entry = $entry; Relative = $name.Substring(10); Length = $entry.Length; Hash = $hash })
    }
    foreach ($file in $files) {
        $ancestor = [IO.Path]::GetDirectoryName($file.Entry.FullName)
        while ($ancestor -and $ancestor -ne 'Hourstone') {
            if ($files.Entry.FullName -contains $ancestor.Replace('\', '/')) { throw 'Archive file/directory collision.' }
            $ancestor = [IO.Path]::GetDirectoryName($ancestor)
        }
    }
    $toc = @($files | Where-Object Relative -CEQ 'Hourstone.toc')
    if ($toc.Count -ne 1) { throw 'Archive is missing its expected Hourstone.toc.' }
    $reader = [IO.StreamReader]::new($toc[0].Entry.Open(), [Text.UTF8Encoding]::new($false, $true))
    try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $versions = [regex]::Matches($text, '(?m)^## Version:\s*(\d+\.\d+\.\d+)\s*$')
    if ($versions.Count -ne 1 -or $versions[0].Groups[1].Value -cne $Version -or
        @([regex]::Matches($text, '(?m)^## X-Hourstone-Sync-Protocol:\s*4\s*$')).Count -ne 1) { throw 'Archive version or protocol-4 capability does not match.' }
    foreach ($line in $text -split '\r?\n') {
        if ($line -and -not $line.StartsWith('#') -and $line.Trim().EndsWith('.lua') -and $files.Relative -cnotcontains $line.Trim()) { throw 'TOC refers to a missing Lua module.' }
    }
    [pscustomobject]@{ Files = $files; TotalBytes = $total }
}
function Expand-AddonPlan($Plan, [string]$Destination) {
    Assert-AddonNoLinks $Destination
    if ([IO.Directory]::Exists($Destination) -or [IO.File]::Exists($Destination)) { throw 'Archive staging directory already exists.' }
    [IO.Directory]::CreateDirectory($Destination) | Out-Null
    foreach ($file in $Plan.Files) {
        $path = Join-Path $Destination $file.Relative
        if (-not (Test-AddonWithin $path $Destination)) { throw 'Archive extraction leaves its staging directory.' }
        Assert-AddonNoLinks $path
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
        $output = [IO.File]::Open($path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        $inputStream = $file.Entry.Open()
        try { $inputStream.CopyTo($output) } finally { $inputStream.Dispose(); $output.Dispose() }
        if ((Get-Item -LiteralPath $path).Length -ne $file.Length -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Hash) { throw 'Extracted file did not match validated archive bytes.' }
    }
}
function Invoke-LocalAddonUpdate {
    [CmdletBinding()]
    param([string[]]$ClientDirectory, [string]$ArchivePath, [string]$ExpectedSha256, [string]$ExpectedVersion, [switch]$Apply)
    if (-not $IsWindows) { throw 'This updater is for Windows only.' }
    if (-not $ClientDirectory -or $ClientDirectory.Count -eq 0) { throw 'Specify each intended -ClientDirectory explicitly. No installations are discovered automatically.' }
    $repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    $archive = [IO.Path]::GetFullPath($ArchivePath); Assert-AddonNoLinks $archive
    $stream = [IO.File]::Open($archive, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        if ($stream.Length -gt 16MB) { throw 'Archive exceeds the compressed size limit.' }
        $sha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()
        if ($sha -cne $ExpectedSha256.ToLowerInvariant()) { throw 'Archive SHA-256 does not match the expected pinned value.' }
        $stream.Position = 0
        $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Read, $true)
        try {
            $plan = Get-AddonArchivePlan $zip $ExpectedVersion
            $backupRoot = [IO.Path]::GetFullPath((Get-AddonBackupRoot)); Assert-AddonNoLinks $backupRoot
            if (Test-AddonWithin $backupRoot $repo) { throw 'Backup root must be outside this repository.' }
            $targets = [Collections.Generic.List[object]]::new(); $unique = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
            foreach ($raw in $ClientDirectory) {
                if (-not [IO.Path]::IsPathFullyQualified($raw) -or $raw.StartsWith('\\')) { throw 'Client directories must be explicit absolute local drive paths.' }
                $client = [IO.Path]::GetFullPath($raw).TrimEnd('\', '/')
                if (-not $unique.Add($client)) { continue }
                if ([IO.Path]::GetFileName($client) -notin @('_retail_', '_classic_', '_classic_era_', '_anniversary_', '_classic_anniversary_')) { throw 'Specify a supported WoW client directory, not the installation root or an account folder.' }
                Assert-AddonNoLinks $client
                if (-not [IO.Directory]::Exists($client) -or -not ([IO.File]::Exists((Join-Path $client 'Wow.exe')) -or [IO.File]::Exists((Join-Path $client 'WowClassic.exe')))) { throw 'The explicit client directory is missing its WoW executable.' }
                Assert-AddonNoLinks (Join-Path $client 'Wow.exe'); Assert-AddonNoLinks (Join-Path $client 'WowClassic.exe')
                $addons = Join-Path $client 'Interface/AddOns'; $target = Join-Path $addons 'Hourstone'
                Assert-AddonNoLinks $target
                if (-not [IO.Directory]::Exists($addons) -or [IO.File]::Exists($target)) { throw 'Expected Interface/AddOns directory is unavailable or Hourstone is not a directory.' }
                if ((Test-AddonWithin $backupRoot ([IO.Path]::GetDirectoryName($client))) -or (Test-AddonWithin $target $backupRoot) -or (Test-AddonWithin $target $repo)) { throw 'Backup, repository and WoW installation bounds overlap.' }
                $existing = [IO.Directory]::Exists($target); $version = $null; $fingerprint = $null
                if ($existing) {
                    $version = Get-AddonVersion (Join-Path $target 'Hourstone.toc')
                    if ([version]$version -gt [version]$ExpectedVersion) { throw 'Refusing to downgrade a newer installed addon.' }
                    $fingerprint = Get-AddonTreeFingerprint $target
                }
                $reason = $null
                try { Assert-AddonClientIdle $client } catch { $reason = $_.Exception.Message }
                $targets.Add([pscustomobject]@{ ClientDirectory = $client; AddonDirectory = $target; ExistingVersion = $version; Status = $(if ($reason) { 'Blocked' } else { 'Ready' }); Reason = $reason; Fingerprint = $fingerprint })
            }
            $report = [ordered]@{ Mode = $(if ($Apply) { 'Apply' } else { 'DryRun' }); ExpectedVersion = $ExpectedVersion; ArchiveSha256 = $sha; FileCount = $plan.Files.Count; UncompressedBytes = $plan.TotalBytes; BackupLocation = '%LOCALAPPDATA%\Hourstone\Backups\Addon'; CanApply = @($targets | Where-Object Status -EQ 'Blocked').Count -eq 0; Targets = @($targets | Select-Object ClientDirectory, AddonDirectory, ExistingVersion, Status, Reason); SavedVariables = 'Never read or modified' }
            if (-not $Apply) { $report | ConvertTo-Json -Depth 5; return }
            if (-not $report.CanApply) { $report | ConvertTo-Json -Depth 5; throw 'Preflight blocked; no files changed.' }
            foreach ($target in $targets) {
                Assert-AddonClientIdle $target.ClientDirectory
                $addons = [IO.Path]::GetDirectoryName($target.AddonDirectory)
                $id = [guid]::NewGuid().ToString('N')
                $backup = Join-Path $backupRoot (([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')) + '-' + $id)
                $staging = Join-Path $addons ('.Hourstone-update-' + $id)
                $rollback = Join-Path $addons ('.Hourstone-rollback-' + $id)
                $oldMoved = $false; $newMoved = $false; $committed = $false
                try {
                    Assert-AddonNoLinks $backup; Assert-AddonNoLinks $staging; Assert-AddonNoLinks $rollback
                    if ($target.Fingerprint) {
                        if ((Get-AddonTreeFingerprint $target.AddonDirectory) -ne $target.Fingerprint) { throw 'Existing addon changed after preflight.' }
                        Assert-AddonClientIdle $target.ClientDirectory
                        Copy-AddonTree $target.AddonDirectory (Join-Path $backup 'Hourstone')
                        if ((Get-AddonTreeFingerprint (Join-Path $backup 'Hourstone')) -ne $target.Fingerprint) { throw 'Addon backup verification failed.' }
                    }
                    Assert-AddonClientIdle $target.ClientDirectory
                    Expand-AddonPlan $plan $staging
                    $stagedFingerprint = Get-AddonTreeFingerprint $staging
                    Assert-AddonClientIdle $target.ClientDirectory
                    Assert-AddonNoLinks $target.AddonDirectory; Assert-AddonNoLinks $rollback
                    if ($target.Fingerprint) {
                        if ((Get-AddonTreeFingerprint $target.AddonDirectory) -ne $target.Fingerprint) { throw 'Existing addon changed while staging.' }
                        [IO.Directory]::Move($target.AddonDirectory, $rollback); $oldMoved = $true
                    }
                    Assert-AddonClientIdle $target.ClientDirectory
                    Assert-AddonNoLinks $staging; Assert-AddonNoLinks $target.AddonDirectory
                    [IO.Directory]::Move($staging, $target.AddonDirectory); $newMoved = $true
                    if ((Get-AddonVersion (Join-Path $target.AddonDirectory 'Hourstone.toc')) -ne $ExpectedVersion -or
                        (Get-AddonTreeFingerprint $target.AddonDirectory) -ne $stagedFingerprint) { throw 'Installed addon verification failed.' }
                    $committed = $true
                    if ($oldMoved) {
                        # After commit, cleanup failure must never replace the good new
                        # addon with an old tree that cleanup may have partly removed.
                        try { Assert-AddonClientIdle $target.ClientDirectory; Remove-AddonOwnedTree $rollback $addons }
                        catch { Write-Warning "Update succeeded; retained old staging for inspection: $rollback" }
                    }
                    [ordered]@{ Status = 'Applied'; ClientDirectory = $target.ClientDirectory; Version = $ExpectedVersion; Backup = $(if ($target.Fingerprint) { Join-Path $backup 'Hourstone' } else { $null }); SavedVariables = 'Never read or modified' } | ConvertTo-Json
                } catch {
                    $failure = $_.Exception.Message
                    if (-not $committed -and ($newMoved -or $oldMoved)) {
                        try {
                            Assert-AddonClientIdle $target.ClientDirectory
                            Assert-AddonNoLinks $target.AddonDirectory; Assert-AddonNoLinks $rollback
                            if ($newMoved) { Remove-AddonOwnedTree $target.AddonDirectory $addons }
                            if ($oldMoved) { [IO.Directory]::Move($rollback, $target.AddonDirectory) }
                        } catch { throw "Update failed; automatic rollback could not safely finish. Retained backup: $backup. Retained rollback: $rollback. Original error: $failure. Rollback error: $($_.Exception.Message)" }
                    }
                    throw "Update failed; any moved original was restored. Retained backup: $backup. Error: $failure"
                } finally {
                    if ([IO.Directory]::Exists($staging)) {
                        try { Assert-AddonClientIdle $target.ClientDirectory; Remove-AddonOwnedTree $staging $addons }
                        catch { Write-Warning 'Staging was retained for manual inspection because safe cleanup could not complete.' }
                    }
                }
            }
        } finally { $zip.Dispose() }
    } finally { $stream.Dispose() }
}

if ($MyInvocation.InvocationName -ne '.') {
    if ($Apply -and $Preflight) { throw 'Choose either -Preflight or -Apply.' }
    Invoke-LocalAddonUpdate -ClientDirectory $ClientDirectory -ArchivePath $ArchivePath -ExpectedSha256 $ExpectedSha256 -ExpectedVersion $ExpectedVersion -Apply:$Apply
}
