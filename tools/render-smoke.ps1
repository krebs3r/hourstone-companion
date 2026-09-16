[CmdletBinding()]
param([string]$Executable, [string]$OutputDirectory, [double[]]$Scales = @(1, 1.5, 2), [ValidateSet('dark', 'light', 'system')][string[]]$Themes = @('dark', 'light'), [switch]$Extended)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $Executable) { $Executable = Join-Path $repoRoot 'src/Hourstone.Companion.App/bin/Release/net10.0-windows/Hourstone.Companion.exe' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts/renders' }
$Executable = [IO.Path]::GetFullPath($Executable)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) { throw 'Build the Release application before rendering.' }
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
function Invoke-RenderCase([string]$Name, [string]$Theme, [double]$Scale, [int]$Width = 1536, [int]$Height = 992, [string[]]$ExtraArguments = @()) {
    $scaleText = $Scale.ToString([Globalization.CultureInfo]::InvariantCulture)
    $output = Join-Path $OutputDirectory ($Name + '.png')
    $reportPath = [IO.Path]::ChangeExtension($output, '.checks.json')
    # A successful process must produce fresh image and assertion evidence.
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
    if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath -Force }
    $arguments = @('--render', ('"' + $output + '"'), '--scale', $scaleText, '--theme', $Theme, '--width', $Width, '--height', $Height, '--verify-render') + $ExtraArguments
    $process = Start-Process -FilePath $Executable -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw "Render timed out: $Name" }
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $output)) { throw "Render failed: $Name" }
    $png = [IO.File]::ReadAllBytes($output)
    if ($png.Length -lt 1024 -or [Convert]::ToHexString($png[0..7]) -ne '89504E470D0A1A0A') { throw 'Invalid rendered PNG.' }
    $widthBytes = [byte[]]$png[16..19]; [Array]::Reverse($widthBytes)
    $heightBytes = [byte[]]$png[20..23]; [Array]::Reverse($heightBytes)
    if ([BitConverter]::ToUInt32($widthBytes) -ne [Math]::Ceiling($Width * $Scale) -or
        [BitConverter]::ToUInt32($heightBytes) -ne [Math]::Ceiling($Height * $Scale)) { throw 'Unexpected render dimensions.' }
    if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) { throw "Render assertions did not run: $Name" }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if ($report.passed -ne $true -or $report.checks -notcontains 'footer-visible-and-unclipped') { throw "Footer verification failed: $Name" }
    if (($ExtraArguments -contains '--selected' -or $ExtraArguments -contains '--removed') -and
        $report.checks -notcontains 'selection-spans-every-column') { throw "Selection verification did not run: $Name" }
    $clientStateIndex = [Array]::IndexOf($ExtraArguments, '--clients-state')
    if ($clientStateIndex -ge 0 -and $report.checks -notcontains ('clients-' + $ExtraArguments[$clientStateIndex + 1] + '-links-visible')) { throw "Client link verification did not run: $Name" }
    Write-Output "PASS render $Name ($($report.checks.Count) layout checks)"
}
foreach ($theme in $Themes) {
    foreach ($scale in $Scales) {
        Invoke-RenderCase -Name "$theme-$($scale.ToString([Globalization.CultureInfo]::InvariantCulture))" -Theme $theme -Scale $scale
    }
}
if ($Extended) {
    foreach ($scale in @(1, 1.5, 2)) {
        Invoke-RenderCase -Name "system-$scale" -Theme system -Scale $scale
        foreach ($theme in @('dark', 'light', 'system')) {
            Invoke-RenderCase -Name "settings-$theme-$scale" -Theme $theme -Scale $scale -ExtraArguments @('--page', 'settings')
        }
    }
    foreach ($theme in @('dark', 'light')) {
        foreach ($scale in @(1, 1.5, 2)) {
            Invoke-RenderCase -Name "removed-$theme-$scale" -Theme $theme -Scale $scale -ExtraArguments @('--removed')
        }
        Invoke-RenderCase -Name "removed-compact-$theme" -Theme $theme -Scale 1 -Width 960 -Height 600 -ExtraArguments @('--removed', '--english')
        Invoke-RenderCase -Name "classes-$theme" -Theme $theme -Scale 1 -Height 1490 -ExtraArguments @('--all-classes')
        Invoke-RenderCase -Name "compact-$theme" -Theme $theme -Scale 1 -Width 960 -Height 600
        Invoke-RenderCase -Name "long-names-$theme" -Theme $theme -Scale 1 -Width 1100 -Height 760 -ExtraArguments @('--long-names', '--english')
        Invoke-RenderCase -Name "settings-draft-$theme" -Theme $theme -Scale 1 -Width 960 -Height 600 -ExtraArguments @('--page', 'settings', '--settings-draft')
    }
}

# Selection is checked from every column, including after focus moves to its action.
if ($Extended) {
    foreach ($theme in @('dark', 'light', 'system')) {
        foreach ($scale in @(1, 1.5, 2)) {
            Invoke-RenderCase -Name "selected-$theme-$scale" -Theme $theme -Scale $scale -ExtraArguments @('--selected', '--selection-column', '2')
        }
    }
    foreach ($column in @(0, 1, 3)) {
        Invoke-RenderCase -Name "selected-column-$column" -Theme dark -Scale 1 -ExtraArguments @('--selected', '--selection-column', "$column")
    }
    foreach ($theme in @('dark', 'light')) {
        Invoke-RenderCase -Name "selected-compact-$theme" -Theme $theme -Scale 1 -Width 960 -Height 600 -ExtraArguments @('--selected', '--selection-unfocused', '--english')
        foreach ($state in @('empty', 'missing', 'outdated')) {
            Invoke-RenderCase -Name "clients-$state-$theme" -Theme $theme -Scale 1 -ExtraArguments @('--clients-state', $state)
            Invoke-RenderCase -Name "clients-$state-compact-$theme" -Theme $theme -Scale 1 -Width 960 -Height 600 -ExtraArguments @('--clients-state', $state, '--english')
        }
    }
}
