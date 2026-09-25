#Requires -Version 7.4
[CmdletBinding()]
param([string]$PackagePath, [string]$SdkBin)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $PackagePath) { $PackagePath = Join-Path $repo 'artifacts/msix/LocalTest/HourstoneCompanion-LocalTest-1.0.0.0-x64.msix' }
$PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$sourceHash = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash
$hashFile = Join-Path (Split-Path -Parent $PackagePath) 'SHA256SUMS'
$expectedLine = "{0}  {1}" -f $sourceHash.ToLowerInvariant(),[IO.Path]::GetFileName($PackagePath)
if (-not (Test-Path -LiteralPath $hashFile) -or $expectedLine -notin (Get-Content -LiteralPath $hashFile)) { throw 'Source MSIX does not match its SHA256SUMS.' }
$signature = Get-AuthenticodeSignature -LiteralPath $PackagePath
if ($signature.SignerCertificate.Subject -ne 'CN=HourstoneCompanion.LocalTest' -or $signature.Status -notin @('Valid','UnknownError','NotTrusted')) { throw 'Source must be the signed LocalTest package.' }
if (-not $SdkBin) { $SdkBin = Join-Path $repo 'artifacts/tool-packages/microsoft.windows.sdk.buildtools/10.0.26100.9169/bin/10.0.26100.0/x64' }
foreach ($tool in @('makeappx.exe','signtool.exe')) { if (-not (Test-Path -LiteralPath (Join-Path $SdkBin $tool) -PathType Leaf)) { throw "Missing SDK tool: $tool" } }
$run = Join-Path $repo ('artifacts/msix-validation/prepared/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($run)
$layout = Join-Path $run 'layout'
& (Join-Path $SdkBin 'makeappx.exe') unpack /p $PackagePath /d $layout /o *> (Join-Path $run 'unpack.log')
if ($LASTEXITCODE) { throw 'Source package could not be unpacked.' }
[xml]$manifest = Get-Content -LiteralPath (Join-Path $layout 'AppxManifest.xml') -Raw
if ($manifest.Package.Identity.Name -ne 'HourstoneCompanion.LocalTest' -or $manifest.Package.Identity.Publisher -ne 'CN=HourstoneCompanion.LocalTest' -or $manifest.Package.Identity.ProcessorArchitecture -ne 'x64') { throw 'Only the separate x64 LocalTest identity is allowed.' }
if ($manifest.Package.Applications.Application.Id -ne 'HourstoneCompanion' -or $manifest.Package.Applications.Application.Executable -ne 'Hourstone.Companion.exe') { throw 'Unexpected application activation entry.' }
$appHash = (Get-FileHash -LiteralPath (Join-Path $layout 'Hourstone.Companion.dll') -Algorithm SHA256).Hash
$rsa = [Security.Cryptography.RSA]::Create(2048)
$request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=HourstoneCompanion.LocalTest', $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
$oids = [Security.Cryptography.OidCollection]::new(); $null = $oids.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
$request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($oids, $false))
$request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new([Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature, $true))
$request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $true))
$certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddDays(2))
$pfx = Join-Path ([IO.Path]::GetTempPath()) ('hourstone-validation-' + [Guid]::NewGuid().ToString('N') + '.pfx')
$packages = @()
try {
    [IO.File]::WriteAllBytes($pfx, $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx))
    [IO.File]::WriteAllBytes((Join-Path $run 'Validation.cer'), $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
    foreach ($version in @('1.0.0.0','1.0.1.0')) {
        $manifest.Package.Identity.SetAttribute('Version', $version)
        $manifest.Save((Join-Path $layout 'AppxManifest.xml'))
        $name = "HourstoneCompanion.LocalTest-$version-x64.msix"; $path = Join-Path $run $name
        & (Join-Path $SdkBin 'makeappx.exe') pack /d $layout /p $path /o *> (Join-Path $run "makeappx-$version.log")
        if ($LASTEXITCODE) { throw "MakeAppx validation failed for $version." }
        & (Join-Path $SdkBin 'signtool.exe') sign /fd SHA256 /f $pfx $path *> (Join-Path $run "signtool-$version.log")
        if ($LASTEXITCODE) { throw "Test signing failed for $version." }
        $packages += [ordered]@{ file=$name; version=$version; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    $plan = [ordered]@{
        formatVersion=1; sourcePackage=$PackagePath; sourceSha256=$sourceHash.ToLowerInvariant(); applicationSha256=$appHash.ToLowerInvariant()
        identity='HourstoneCompanion.LocalTest'; applicationId='HourstoneCompanion'; publisher=$certificate.Subject
        certificateFile='Validation.cer'; certificateSha256=(Get-FileHash -LiteralPath (Join-Path $run 'Validation.cer') -Algorithm SHA256).Hash.ToLowerInvariant()
        thumbprint=$certificate.Thumbprint; notBeforeUtc=$certificate.NotBefore.ToUniversalTime().ToString('o'); notAfterUtc=$certificate.NotAfter.ToUniversalTime().ToString('o')
        trustStore='LocalMachine/TrustedPeople'; packages=$packages; privateKeyRetained=$false
        tests=@('install','packaged activation with isolated LocalState profile','upgrade preserves isolated profile','packaged activation after upgrade','remove package','restore initial certificate trust')
        notCovered=@('StartupTask enable/disable','guided legacy import','simultaneous real distribution instances','WACK','Store certification')
    }
    $planPath = Join-Path $run 'test-plan.json'
    $plan | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $planPath -Encoding utf8NoBOM
    $packages | ForEach-Object { "{0}  {1}" -f $_.sha256,$_.file } | Set-Content -LiteralPath (Join-Path $run 'SHA256SUMS') -Encoding utf8NoBOM
    Write-Output "Prepared: $planPath"
    Write-Output "Plan SHA256: $((Get-FileHash -LiteralPath $planPath -Algorithm SHA256).Hash.ToLowerInvariant())"
    Write-Output "Certificate: $($certificate.Thumbprint); public certificate only; valid until $($plan.notAfterUtc)."
    Write-Output 'No certificate store, installed package, autostart, or Windows security setting was changed.'
} finally {
    if (Test-Path -LiteralPath $pfx) { Remove-Item -LiteralPath $pfx -Force }
    $certificate.Dispose(); $rsa.Dispose()
}
