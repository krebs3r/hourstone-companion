#Requires -Version 7.4
[CmdletBinding()]
param(
    [ValidateSet('LocalTest', 'Store')][string]$Profile = 'LocalTest',
    [string]$IdentityPath,
    [string]$SdkBin,
    [switch]$SignLocalTest
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = Join-Path $repoRoot 'artifacts'
$packageRoot = Join-Path $artifactRoot "msix/$Profile"
$layout = Join-Path $packageRoot 'layout'
$project = Join-Path $repoRoot 'src/Hourstone.Companion.App/Hourstone.Companion.App.csproj'
if ($Profile -eq 'Store') {
    if ($SignLocalTest) { throw 'A local test certificate cannot sign a Store production package.' }
    if (-not $IdentityPath -or -not (Test-Path -LiteralPath $IdentityPath -PathType Leaf)) { throw 'Store packaging requires an identity file exported from the reserved Partner Center product. No placeholder identity is accepted.' }
    $identity = Get-Content -LiteralPath $IdentityPath -Raw | ConvertFrom-Json
    foreach ($field in @('name', 'publisher', 'publisherDisplayName', 'storeProductId', 'packageVersion')) {
        if (-not $identity.PSObject.Properties[$field] -or [string]::IsNullOrWhiteSpace($identity.$field)) { throw "Missing Store identity field: $field" }
        if ($identity.$field -match '(?i)placeholder|requires-build|example|localtest|contoso') { throw "Placeholder Store identity is not accepted: $field" }
    }
} else {
    if ($IdentityPath) { throw 'LocalTest never uses a production identity file.' }
    $identity = [pscustomobject]@{ name = 'HourstoneCompanion.LocalTest'; publisher = 'CN=HourstoneCompanion.LocalTest'; publisherDisplayName = 'Hourstone local test'; storeProductId = ''; packageVersion = '1.0.0.0' }
}
if ($identity.name -notmatch '^[A-Za-z0-9.-]{3,50}$' -or $identity.publisher -notmatch '^CN=' -or
    ($Profile -eq 'Store' -and $identity.storeProductId -notmatch '^[A-Za-z0-9]{8,32}$')) { throw 'Invalid MSIX identity.' }
$packageVersion = [version]$identity.packageVersion
$invalidVersionParts = @(@($packageVersion.Major, $packageVersion.Minor, $packageVersion.Build) | Where-Object { $_ -gt 65535 })
if ($packageVersion.Major -lt 1 -or $packageVersion.Revision -ne 0 -or $packageVersion.Minor -lt 0 -or $packageVersion.Build -lt 0 -or
    $invalidVersionParts.Count -gt 0) { throw 'Store package version requires four components, major > 0, fourth component 0, each <= 65535.' }
function Invoke-DotNet([string[]]$Arguments) { & dotnet @Arguments; if ($LASTEXITCODE) { throw 'MSIX build dependency or publish failed.' } }
if (-not $SdkBin) {
    $toolPackages = Join-Path $artifactRoot 'tool-packages'
    Invoke-DotNet @('restore', (Join-Path $repoRoot 'packaging/StoreTools.csproj'), '--locked-mode', '--packages', $toolPackages)
    $toolRoot = Join-Path $toolPackages 'microsoft.windows.sdk.buildtools/10.0.26100.9169'
    $makeAppx = Get-ChildItem -LiteralPath $toolRoot -Recurse -Filter makeappx.exe -File | Where-Object { $_.Directory.Name -eq 'x64' } | Select-Object -First 1
    if (-not $makeAppx) { throw 'The pinned Microsoft Windows SDK BuildTools package does not contain x64 MakeAppx.' }
    $SdkBin = $makeAppx.Directory.FullName
}
if (-not (Test-Path -LiteralPath (Join-Path $SdkBin 'makeappx.exe'))) { throw 'MakeAppx.exe was not found.' }
$resolved = [IO.Path]::GetFullPath($packageRoot)
if (-not $resolved.StartsWith([IO.Path]::GetFullPath($artifactRoot) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid MSIX artifact directory.' }
if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
[IO.Directory]::CreateDirectory($layout) | Out-Null
Invoke-DotNet @('restore', $project, '--runtime', 'win-x64', '--locked-mode')
Invoke-DotNet @('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '--no-restore', '-m:1', '/nodeReuse:false', '-p:UseSharedCompilation=false', '-p:DebugType=None', '-p:DebugSymbols=false', '-p:ContinuousIntegrationBuild=true', '-o', $layout)
foreach ($file in Get-ChildItem -LiteralPath $layout -Recurse -File) {
    if ($file.Extension -notin @('.dll', '.exe') -and $file.Name -notmatch '\.(deps|runtimeconfig)\.json$') { Remove-Item -LiteralPath $file.FullName -Force }
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $layout 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/ASSETS.md') -Destination (Join-Path $layout 'ASSET-NOTICES.txt')
& python (Join-Path $PSScriptRoot 'dependency-notices.py') --output $layout
if ($LASTEXITCODE) { throw 'Dependency notices failed.' }
$identity | Select-Object storeProductId | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $layout 'distribution.json') -Encoding utf8NoBOM
[xml]$manifest = Get-Content -LiteralPath (Join-Path $repoRoot 'packaging/AppxManifest.xml') -Raw
$manifest.Package.Identity.SetAttribute('Name', $identity.name)
$manifest.Package.Identity.SetAttribute('Publisher', $identity.publisher)
$manifest.Package.Identity.SetAttribute('Version', $identity.packageVersion)
$manifest.Package.Properties.PublisherDisplayName = $identity.publisherDisplayName
if ($Profile -eq 'LocalTest') { $manifest.Package.Properties.DisplayName = 'Hourstone Companion (Local Test)'; $manifest.Package.Applications.Application.VisualElements.SetAttribute('DisplayName', 'Hourstone Companion (Local Test)') }
$manifest.Save((Join-Path $layout 'AppxManifest.xml'))
Add-Type -AssemblyName System.Drawing
$assets = Join-Path $layout 'Assets'; [IO.Directory]::CreateDirectory($assets) | Out-Null
$sourceIcon = [Drawing.Image]::FromFile((Join-Path $repoRoot 'src/Hourstone.Companion.App/Assets/Logo.png'))
try {
    foreach ($asset in @(@('StoreLogo.png', 50), @('Square44x44Logo.png', 44), @('Square150x150Logo.png', 150))) {
        $size = [int]$asset[1]; $bitmap = [Drawing.Bitmap]::new($size, $size); $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([Drawing.Color]::Transparent); $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $ratio = [Math]::Min($size / $sourceIcon.Width, $size / $sourceIcon.Height); $width = [int]($sourceIcon.Width * $ratio); $height = [int]($sourceIcon.Height * $ratio)
            $graphics.DrawImage($sourceIcon, [int](($size - $width) / 2), [int](($size - $height) / 2), $width, $height)
            $bitmap.Save((Join-Path $assets $asset[0]), [Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $sourceIcon.Dispose() }
$msix = Join-Path $packageRoot "HourstoneCompanion-$Profile-$($identity.packageVersion)-x64.msix"
$makeAppxLog = Join-Path $packageRoot 'makeappx.log'
& (Join-Path $SdkBin 'makeappx.exe') pack /d $layout /p $msix /o *> $makeAppxLog
if ($LASTEXITCODE) { Get-Content -LiteralPath $makeAppxLog -Tail 20; throw 'MSIX manifest validation or package creation failed.' }
Write-Output 'PASS MakeAppx manifest validation and package creation.'
if ($SignLocalTest) {
    $rsa = [Security.Cryptography.RSA]::Create(2048)
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new($identity.publisher, $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $oids = [Security.Cryptography.OidCollection]::new(); $null = $oids.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
    $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($oids, $false))
    $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new([Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature, $true))
    $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddDays(14))
    $pfx = Join-Path ([IO.Path]::GetTempPath()) ('hourstone-msix-' + [Guid]::NewGuid().ToString('N') + '.pfx')
    try {
        [IO.File]::WriteAllBytes($pfx, $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx))
        [IO.File]::WriteAllBytes((Join-Path $packageRoot 'LocalTest.cer'), $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
        & (Join-Path $SdkBin 'signtool.exe') sign /fd SHA256 /f $pfx $msix
        if ($LASTEXITCODE) { throw 'Local test signing failed.' }
    } finally { if (Test-Path -LiteralPath $pfx) { Remove-Item -LiteralPath $pfx -Force }; $certificate.Dispose(); $rsa.Dispose() }
}
$hash = (Get-FileHash -LiteralPath $msix -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($msix))" | Set-Content -LiteralPath (Join-Path $packageRoot 'SHA256SUMS') -Encoding utf8NoBOM
[ordered]@{ profile = $Profile; packageVersion = $identity.packageVersion; identity = $identity.name; artifact = [IO.Path]::GetFileName($msix); sha256 = $hash; locallySigned = [bool]$SignLocalTest; storeSubmitted = $false; installedAndTested = $false; wackPassed = $false } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageRoot 'package-validation.json') -Encoding utf8NoBOM
Write-Output "Created $msix. No certificate trust was installed, no app was installed, and nothing was submitted to the Store."
