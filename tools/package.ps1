#Requires -Version 7.4
[CmdletBinding()]
param([switch]$Unsigned, [string]$Version)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$project = Join-Path $repoRoot 'src/Hourstone.Companion.App/Hourstone.Companion.App.csproj'
$declaredVersion = ([xml](Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
if (-not $Version) { $Version = $declaredVersion }
if ($Version -ne $declaredVersion -or $Version -notmatch '^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$') { throw 'Package version must match Directory.Build.props.' }
if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -ne "v$Version") { throw 'Release tag must match the package version.' }
if ($Unsigned -and $env:GITHUB_REF_TYPE -eq 'tag') { throw 'Unsigned packages cannot be built for a public release tag.' }
if (-not $Unsigned -and -not $env:SIGNING_CERTIFICATE_THUMBPRINT -and
    -not ($env:SIGNING_CERTIFICATE_BASE64 -and $env:SIGNING_CERTIFICATE_PASSWORD)) {
    throw 'Public packaging requires a signing certificate. Use -Unsigned only for local test packages.'
}
function Invoke-DotNet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw 'The .NET build or packaging tool failed.' }
}
function Reset-ArtifactDirectory([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    if (-not $resolved.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to reset a directory outside generated artifacts.'
    }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
    [IO.Directory]::CreateDirectory($resolved) | Out-Null
}
function Assert-Signature([string]$Path) {
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid') { throw "Executable signature validation failed: $([IO.Path]::GetFileName($Path))" }
}
$publishRoot = Join-Path $artifactRoot 'publish'
$packageRoot = Join-Path $artifactRoot 'package'
$releaseRoot = Join-Path $artifactRoot 'releases'
$certificate = $null
$removeCertificate = $false
$pfxPath = $null
Push-Location $repoRoot
try {
    & python (Join-Path $PSScriptRoot 'verify_assets.py')
    if ($LASTEXITCODE -ne 0) { throw 'Bundled asset verification failed.' }
    if (-not $Unsigned) {
        if ($env:SIGNING_CERTIFICATE_THUMBPRINT) {
            $thumbprint = $env:SIGNING_CERTIFICATE_THUMBPRINT -replace '\s', ''
            if ($thumbprint -notmatch '^[A-Fa-f0-9]{40}$') { throw 'Invalid signing certificate thumbprint.' }
            $certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$thumbprint"
        } else {
            $pfxPath = Join-Path ([IO.Path]::GetTempPath()) ("hourstone-sign-" + [Guid]::NewGuid().ToString('N') + '.pfx')
            [IO.File]::WriteAllBytes($pfxPath, [Convert]::FromBase64String($env:SIGNING_CERTIFICATE_BASE64))
            $password = ConvertTo-SecureString $env:SIGNING_CERTIFICATE_PASSWORD -AsPlainText -Force
            $previewCertificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
                $pfxPath, $env:SIGNING_CERTIFICATE_PASSWORD, [Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
            $existing = Test-Path -LiteralPath ("Cert:\CurrentUser\My\" + $previewCertificate.Thumbprint)
            $previewCertificate.Dispose()
            $certificate = Import-PfxCertificate -FilePath $pfxPath -Password $password -CertStoreLocation 'Cert:\CurrentUser\My'
            $removeCertificate = -not $existing
        }
        if (-not $certificate.HasPrivateKey -or $certificate.NotAfter.ToUniversalTime() -le [DateTime]::UtcNow) {
            throw 'Signing requires a current certificate with an accessible private key.'
        }
    }
    Reset-ArtifactDirectory $publishRoot
    Reset-ArtifactDirectory $packageRoot
    Reset-ArtifactDirectory $releaseRoot
    Invoke-DotNet @('tool', 'restore')
    Invoke-DotNet @('restore', $project, '--runtime', 'win-x64', '--locked-mode')
    Invoke-DotNet @('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '--no-restore',
        '-p:DebugType=None', '-p:DebugSymbols=false', '-p:ContinuousIntegrationBuild=true', '-o', $publishRoot)
    # Only compiled runtime files and runtime configuration enter the staging directory.
    foreach ($file in Get-ChildItem -LiteralPath $publishRoot -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath($publishRoot, $file.FullName)
        $allowed = $file.Extension -in @('.dll', '.exe') -or
            $file.Name -match '\.(deps|runtimeconfig)\.json$' -or
            $file.Name -in @('LICENSE.txt', 'THIRD-PARTY-NOTICES.TXT')
        if (-not $allowed) { continue }
        $target = Join-Path $packageRoot $relative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $packageRoot 'LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/ASSETS.md') -Destination (Join-Path $packageRoot 'ASSET-NOTICES.txt')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/assets-manifest.json') -Destination (Join-Path $packageRoot 'assets-manifest.json')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/app-icon.json') -Destination (Join-Path $packageRoot 'app-icon.json')
    & python (Join-Path $PSScriptRoot 'dependency-notices.py') --output $packageRoot
    if ($LASTEXITCODE -ne 0) { throw 'Dependency inventory generation failed.' }
    & (Join-Path $PSScriptRoot 'render-smoke.ps1') -Executable (Join-Path $packageRoot 'Hourstone.Companion.exe') -OutputDirectory (Join-Path $artifactRoot 'package-renders')
    $packArgs = @('tool', 'run', 'vpk', '--', 'pack', '--packId', 'HourstoneCompanion',
        '--packVersion', $Version, '--packDir', $packageRoot, '--outputDir', $releaseRoot,
        '--mainExe', 'Hourstone.Companion.exe', '--packTitle', 'Hourstone Companion',
        '--packAuthors', 'krebs3r', '--runtime', 'win-x64', '--channel', 'win', '--delta', 'None',
        '--icon', (Join-Path $repoRoot 'src/Hourstone.Companion.App/Assets/Hourstone.ico'),
        '--releaseNotes', (Join-Path $repoRoot 'docs/RELEASE-NOTES.md'))
    if (-not $Unsigned) {
        $packArgs += @('--signParams', "/sha1 $($certificate.Thumbprint) /s My /fd SHA256 /tr http://timestamp.digicert.com /td SHA256")
    }
    Invoke-DotNet $packArgs
    $releaseFiles = @(Get-ChildItem -LiteralPath $releaseRoot -File)
    if (-not ($releaseFiles.Name -match 'Setup\.exe$') -or -not ($releaseFiles.Name -match '-full\.nupkg$') -or
        -not ($releaseFiles.Name -contains 'releases.win.json')) { throw 'Velopack did not produce the expected installer and update feed.' }
    $assetPattern = '^(HourstoneCompanion[-.A-Za-z0-9]*\.(exe|nupkg|zip)|RELEASES|releases\.win\.json|assets\.win\.json)$'
    foreach ($file in $releaseFiles) {
        if ($file.Name -notmatch $assetPattern) { throw "Unexpected release output: $($file.Name)" }
        if (-not $Unsigned -and $file.Extension -eq '.exe') { Assert-Signature $file.FullName }
        if ($file.Extension -in @('.nupkg', '.zip')) {
            $zip = [IO.Compression.ZipFile]::OpenRead($file.FullName)
            try {
                foreach ($entry in $zip.Entries) {
                    if ($entry.FullName -match '(?i)(^|/)(\.git|\.env|SavedVariables)(/|$)|\.(pdb|cs|pfx|pem|sqlite|db|log)$') {
                        throw "Disallowed file in package: $($entry.FullName)"
                    }
                    if (-not $Unsigned -and $entry.Name -match '(?i)\.exe$') {
                        $validationRoot = Join-Path $artifactRoot 'signature-check'
                        [IO.Directory]::CreateDirectory($validationRoot) | Out-Null
                        $validationFile = Join-Path $validationRoot ([Guid]::NewGuid().ToString('N') + '.exe')
                        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $validationFile)
                        try { Assert-Signature $validationFile } finally { Remove-Item -LiteralPath $validationFile -Force }
                    }
                }
            } finally { $zip.Dispose() }
        }
    }
    $portableRoot = Join-Path $artifactRoot 'portable-smoke'
    Reset-ArtifactDirectory $portableRoot
    $portableArchive = Join-Path $releaseRoot 'HourstoneCompanion-win-Portable.zip'
    [IO.Compression.ZipFile]::ExtractToDirectory($portableArchive, $portableRoot)
    & (Join-Path $PSScriptRoot 'render-smoke.ps1') -Executable (Join-Path $portableRoot 'current/Hourstone.Companion.exe') -OutputDirectory (Join-Path $artifactRoot 'portable-renders') -Scales 1 -Themes 'dark'
    foreach ($name in @('dependencies.cdx.json', 'DEPENDENCY-NOTICES.txt', 'ASSET-NOTICES.txt', 'assets-manifest.json', 'app-icon.json', 'LICENSE.txt')) {
        Copy-Item -LiteralPath (Join-Path $packageRoot $name) -Destination (Join-Path $releaseRoot $name)
    }
    $entries = @(Get-ChildItem -LiteralPath $releaseRoot -File | Sort-Object Name | ForEach-Object {
        @{ file = $_.Name; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    $newline = [string][char]10
    $checksums = ($entries | ForEach-Object { $_.sha256 + '  ' + $_.file }) -join $newline
    [IO.File]::WriteAllText((Join-Path $releaseRoot 'SHA256SUMS'), $checksums + $newline, [Text.UTF8Encoding]::new($false))
    $entries += @{ file = 'SHA256SUMS'; sha256 = (Get-FileHash -LiteralPath (Join-Path $releaseRoot 'SHA256SUMS')).Hash.ToLowerInvariant() }
    $manifest = @{ formatVersion = 1; version = $Version; runtime = 'win-x64'; signed = -not $Unsigned; assets = $entries }
    [IO.File]::WriteAllText((Join-Path $releaseRoot 'release-manifest.json'), ($manifest | ConvertTo-Json -Depth 5) + $newline, [Text.UTF8Encoding]::new($false))
    Write-Output "PASS package $Version (signed: $(-not $Unsigned)); assets in artifacts/releases"
} finally {
    if ($removeCertificate -and $certificate) { Remove-Item -LiteralPath ("Cert:\CurrentUser\My\" + $certificate.Thumbprint) -Force }
    if ($pfxPath -and (Test-Path -LiteralPath $pfxPath)) { Remove-Item -LiteralPath $pfxPath -Force }
    Pop-Location
}
