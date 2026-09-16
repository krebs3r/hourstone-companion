[CmdletBinding()]
param([Parameter(Mandatory)][string]$Tag)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$releaseRoot = Join-Path $repoRoot 'artifacts/releases'
$manifestPath = Join-Path $releaseRoot 'release-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.formatVersion -ne 1 -or -not $manifest.signed -or $Tag -ne "v$($manifest.version)") {
    throw 'Only a validated signed package with a matching tag can be published.'
}
$assets = @()
foreach ($entry in $manifest.assets) {
    if ($entry.file -notmatch '^(HourstoneCompanion[-.A-Za-z0-9]*\.(exe|nupkg|zip)|RELEASES|releases\.win\.json|assets\.win\.json|dependencies\.cdx\.json|DEPENDENCY-NOTICES\.txt|ASSET-NOTICES\.txt|assets-manifest\.json|app-icon\.json|LICENSE\.txt|SHA256SUMS)$') { throw 'Unexpected release asset.' }
    if ($entry.file -ne [IO.Path]::GetFileName($entry.file)) { throw 'Invalid release asset path.' }
    $asset = Join-Path $releaseRoot $entry.file
    if ((Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) {
        throw "Release asset checksum mismatch: $($entry.file)"
    }
    if ($entry.file -match 'Setup\.exe$' -and (Get-AuthenticodeSignature -LiteralPath $asset).Status -ne 'Valid') {
        throw 'Installer signature is not valid.'
    }
    $assets += $asset
}
if (-not ($assets -match 'Setup\.exe$')) { throw 'The signed installer is missing.' }
$assets += $manifestPath
& gh release create $Tag @assets --repo krebs3r/hourstone-companion --verify-tag --title "Hourstone Companion $($manifest.version)" --notes-file (Join-Path $repoRoot 'docs/RELEASE-NOTES.md')
if ($LASTEXITCODE -ne 0) { throw 'GitHub release publication failed.' }
