[CmdletBinding()]
param([string]$Executable, [string]$OutputDirectory, [double[]]$Scales = @(1, 1.5, 2), [string[]]$Themes = @('dark', 'light'))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $Executable) { $Executable = Join-Path $repoRoot 'src/Hourstone.Companion.App/bin/Release/net10.0-windows/Hourstone.Companion.exe' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts/renders' }
$Executable = [IO.Path]::GetFullPath($Executable)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) { throw 'Build the Release application before rendering.' }
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
foreach ($theme in $Themes) {
    foreach ($scale in $Scales) {
        $scaleText = $scale.ToString([Globalization.CultureInfo]::InvariantCulture)
        $output = Join-Path $OutputDirectory "$theme-$scaleText.png"
        $arguments = @('--render', ('"' + $output + '"'), '--scale', $scaleText, '--theme', $theme, '--width', '1536', '--height', '992')
        $process = Start-Process -FilePath $Executable -ArgumentList $arguments -WindowStyle Hidden -PassThru
        if (-not $process.WaitForExit(60000)) { $process.Kill(); throw "Render timed out: $theme at $scaleText" }
        if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $output)) { throw "Render failed: $theme at $scaleText" }
        $png = [IO.File]::ReadAllBytes($output)
        if ($png.Length -lt 1024 -or [Convert]::ToHexString($png[0..7]) -ne '89504E470D0A1A0A') { throw 'Invalid rendered PNG.' }
        $widthBytes = [byte[]]$png[16..19]; [Array]::Reverse($widthBytes)
        $heightBytes = [byte[]]$png[20..23]; [Array]::Reverse($heightBytes)
        if ([BitConverter]::ToUInt32($widthBytes) -ne [Math]::Ceiling(1536 * $scale) -or
            [BitConverter]::ToUInt32($heightBytes) -ne [Math]::Ceiling(992 * $scale)) { throw 'Unexpected render dimensions.' }
        Write-Output "PASS render $theme at $scaleText"
    }
}
