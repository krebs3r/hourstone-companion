[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][string]$DataDirectory,
    [ValidateSet('VelopackInstalled', 'VelopackPortable')][string]$ExpectedDistribution
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Executable = [IO.Path]::GetFullPath($Executable)
$DataDirectory = [IO.Path]::GetFullPath($DataDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)
$legacyDirectory = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Hourstone/Companion'))
if ($DataDirectory.Equals($legacyDirectory, [StringComparison]::OrdinalIgnoreCase) -or
    $DataDirectory.StartsWith($legacyDirectory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Startup smoke tests cannot use the existing Hourstone profile or one of its subdirectories.'
}
if (Test-Path -LiteralPath $DataDirectory) { throw 'DataDirectory must be a new, isolated directory. Existing paths are never reused.' }
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) { throw 'The packaged executable does not exist.' }
$packageDirectory = Split-Path -Parent $Executable
if (-not (Test-Path -LiteralPath (Join-Path $packageDirectory 'Update.exe') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $packageDirectory 'current/Hourstone.Companion.exe') -PathType Leaf)) {
    throw 'Executable must be the Velopack root launcher beside Update.exe and current, not the application inside current.'
}
if (-not $ExpectedDistribution) {
    $ExpectedDistribution = if (Test-Path -LiteralPath (Join-Path $packageDirectory '.portable') -PathType Leaf) { 'VelopackPortable' } else { 'VelopackInstalled' }
}
# Refuse junctions/symlinks so a seemingly isolated path cannot resolve into a real profile.
for ($ancestor = $DataDirectory; $ancestor; $ancestor = [IO.Path]::GetDirectoryName($ancestor)) {
    if ((Test-Path -LiteralPath $ancestor) -and
        ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'DataDirectory and its ancestors must not contain reparse points.'
    }
}
New-Item -ItemType Directory -Path $DataDirectory -ErrorAction Stop | Out-Null
$reportPath = Join-Path $DataDirectory 'smoke-result.json'
$arguments = @('--smoke-test', '--data-directory', ('"' + $DataDirectory + '"'))
$process = Start-Process -FilePath $Executable -ArgumentList $arguments -WorkingDirectory $packageDirectory -WindowStyle Hidden -PassThru
$watch = [Diagnostics.Stopwatch]::StartNew()
$report = $null
while ($watch.Elapsed.TotalSeconds -lt 45) {
    if (Test-Path -LiteralPath $reportPath -PathType Leaf) {
        # The root launcher can exit before its child; a fresh child report is the completion signal.
        try { $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json -ErrorAction Stop }
        catch { $report = $null }
        if ($null -ne $report) { break }
    }
    Start-Sleep -Milliseconds 200
}
if ($null -eq $report) { throw "Startup smoke timed out after 45 seconds. Launcher PID: $($process.Id). Evidence directory: $DataDirectory" }
foreach ($field in @('passed', 'databaseCreated', 'windowVisible', 'dispatcherRunning')) {
    if ($report.$field -isnot [bool] -or $report.$field -ne $true) { throw "Startup smoke failed: $field. Report: $reportPath" }
}
if ($report.distribution -cne $ExpectedDistribution) { throw "Unexpected distribution '$($report.distribution)'; expected '$ExpectedDistribution'." }
if (-not ([IO.Path]::GetFullPath([string]$report.dataDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)).Equals($DataDirectory, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The application did not use the requested isolated data directory.'
}
if (-not (Test-Path -LiteralPath (Join-Path $DataDirectory 'companion.sqlite') -PathType Leaf)) { throw 'The application did not create its test database.' }
$process.Refresh()
if ($process.HasExited -and $process.ExitCode -ne 0) { throw "The root launcher failed with exit code $($process.ExitCode)." }
Write-Output "PASS startup $($ExpectedDistribution): window, dispatcher, sync and isolated database. Report: $reportPath"
