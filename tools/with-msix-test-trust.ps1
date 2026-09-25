#Requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PlanPath,
    [Parameter(Mandatory)][string]$ExpectedPlanSha256,
    [Parameter(Mandatory)][string]$ExpectedUserSid,
    [Parameter(Mandatory)][string]$LeaseDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ([Security.Principal.WindowsIdentity]::GetCurrent().User.Value -ne $ExpectedUserSid) { throw 'The original Windows user is required.' }
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'This certificate-only helper needs administrator rights.' }
$PlanPath = (Resolve-Path -LiteralPath $PlanPath).Path
if ((Get-FileHash -LiteralPath $PlanPath -Algorithm SHA256).Hash -ne $ExpectedPlanSha256) { throw 'Reviewed test plan hash mismatch.' }
$planDirectory = Split-Path -Parent $PlanPath
$LeaseDirectory = [IO.Path]::GetFullPath($LeaseDirectory)
if (-not $LeaseDirectory.StartsWith($planDirectory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $LeaseDirectory)) { throw 'Trust lease requires a new directory inside this test plan.' }
for ($item = $LeaseDirectory; $item; $item = [IO.Path]::GetDirectoryName($item)) {
    if ((Test-Path -LiteralPath $item) -and ((Get-Item -LiteralPath $item -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Trust lease does not accept reparse points.' }
}
# The shared preflight validates package hashes, signatures, certificate EKU/validity, and fixed LocalTest identity.
& (Join-Path $PSScriptRoot 'test-msix-native.ps1') -PlanPath $PlanPath -ExpectedPlanSha256 $ExpectedPlanSha256
if (-not $?) { throw 'Native test preflight failed.' }
$plan = Get-Content -LiteralPath $PlanPath -Raw | ConvertFrom-Json
$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new((Join-Path $planDirectory $plan.certificateFile))
$storePath = 'Cert:\LocalMachine\TrustedPeople\' + $certificate.Thumbprint
if (Test-Path -LiteralPath $storePath) { throw 'Certificate is already trusted; this helper will not take ownership of pre-existing trust.' }
$null = [IO.Directory]::CreateDirectory($LeaseDirectory)
$lease = [ordered]@{ userSid=$ExpectedUserSid; planSha256=$ExpectedPlanSha256; thumbprint=$certificate.Thumbprint; store=$storePath; active=$false; removed=$false; released=$false; expiresAtUtc=[DateTime]::UtcNow.AddMinutes(5).ToString('o'); error=$null }
function Write-Lease { $lease | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $LeaseDirectory 'trust-lease.json') -Encoding utf8NoBOM }
Write-Lease
$added = $false
try {
    $store = [Security.Cryptography.X509Certificates.X509Store]::new('TrustedPeople','LocalMachine')
    try { $store.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite); $added=$true; $store.Add($certificate) } finally { $store.Dispose() }
    $lease.active = $true; Write-Lease
    # Never launches an app. The normal, unelevated runner performs the separate MSIX test.
    do {
        Start-Sleep -Milliseconds 250
        $lease.released = Test-Path -LiteralPath (Join-Path $LeaseDirectory 'release')
    } while (-not $lease.released -and [DateTime]::UtcNow -lt [DateTime]::Parse($lease.expiresAtUtc).ToUniversalTime())
    if (-not $lease.released) { $lease.error='The five-minute trust lease expired before the normal runner released it.' }
} catch { $lease.error=$_.Exception.Message }
finally {
    try {
        if ($added -and (Test-Path -LiteralPath $storePath)) { Remove-Item -LiteralPath $storePath -ErrorAction Stop }
        $lease.removed = -not (Test-Path -LiteralPath $storePath)
    } catch { $lease.error = ($lease.error + ' Cleanup: ' + $_.Exception.Message).Trim() }
    $lease.active = $false; Write-Lease; $certificate.Dispose()
}
if (-not $lease.removed -or $lease.error) { throw "Trust lease did not finish cleanly: $($lease.error)" }
