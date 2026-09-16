[CmdletBinding()]
param([Parameter(Mandatory)][string]$Tag, [string]$Commit, [switch]$AllowUnsigned)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$repository = 'krebs3r/hourstone-companion'
$releaseRoot = Join-Path $repoRoot 'artifacts/releases'
$manifestPath = Join-Path $releaseRoot 'release-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.formatVersion -ne 1 -or $manifest.signed -isnot [bool] -or
    $manifest.runtime -ne 'win-x64' -or $manifest.version -notmatch '^\d+\.\d+\.\d+$' -or
    $Tag -cne "v$($manifest.version)") { throw 'Invalid release manifest or matching stable version tag.' }
if (-not $manifest.signed -and -not $AllowUnsigned) {
    throw 'Only a validated signed package is published by default. Explicit -AllowUnsigned is required for unsigned publication.'
}
$declaredVersion = ([xml](Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
if ($manifest.version -ne $declaredVersion) { throw 'Release manifest version must match Directory.Build.props.' }
. (Join-Path $PSScriptRoot 'release-assets.ps1')
$requiredNames = @(Get-PublicReleasePayloadNames $manifest.version) + 'SHA256SUMS'
$assets = [Collections.Generic.List[string]]::new()
$expectedSizes = [Collections.Generic.Dictionary[string,long]]::new([StringComparer]::Ordinal)
foreach ($entry in $manifest.assets) {
    if ($entry.file -cnotin $requiredNames -or $entry.file -ne [IO.Path]::GetFileName($entry.file)) { throw 'Unexpected release asset.' }
    if ($expectedSizes.ContainsKey($entry.file)) { throw 'Duplicate release asset.' }
    if ($entry.sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Invalid release asset checksum.' }
    $asset = Join-Path $releaseRoot $entry.file
    $file = Get-Item -LiteralPath $asset
    if ($file -isnot [IO.FileInfo] -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Release assets must be regular files.' }
    if ((Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash -ne $entry.sha256) {
        throw "Release asset checksum mismatch: $($entry.file)"
    }
    if ($manifest.signed -and $entry.file -eq 'HourstoneCompanion-win-Setup.exe' -and
        (Get-AuthenticodeSignature -LiteralPath $asset).Status -ne 'Valid') { throw 'Installer signature is not valid.' }
    $assets.Add($asset)
    $expectedSizes.Add($entry.file, $file.Length)
}
if (@($requiredNames | Where-Object { -not $expectedSizes.ContainsKey($_) }).Count -gt 0) { throw 'The complete installer, portable package, update feed and checksums are required.' }
Assert-PublicReleaseChecksums -ReleaseDirectory $releaseRoot -Entries $manifest.assets -Version $manifest.version
$notesPath = Join-Path $repoRoot 'docs/RELEASE-NOTES.md'
if (-not (Test-Path -LiteralPath $notesPath -PathType Leaf)) { throw 'Release notes are missing.' }
$head = (& git -C $repoRoot rev-parse HEAD | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $head -notmatch '^[a-fA-F0-9]{40}$') { throw 'Cannot resolve the checked-out release commit.' }
if (-not $Commit) { $Commit = $head }
if ($Commit -notmatch '^[a-fA-F0-9]{40}$' -or $Commit -ne $head) { throw 'Release target must match the complete checked-out commit ID.' }

function Invoke-GitHub([string[]]$Arguments) {
    $output = & gh @Arguments
    if ($LASTEXITCODE -ne 0) { throw 'GitHub release operation failed; any draft remains unpublished.' }
    return ($output | Out-String).Trim()
}
function Read-GitHub([string[]]$Arguments) {
    $json = Invoke-GitHub $Arguments
    if (-not $json) { throw 'GitHub returned no release metadata.' }
    return ConvertFrom-Json -InputObject $json -NoEnumerate
}
function Read-Release {
    $pages = Read-GitHub @('api', "repos/$repository/releases?per_page=100", '--paginate', '--slurp')
    $matches = @($pages | ForEach-Object { $_ } | Where-Object { $_.tag_name -ceq $Tag })
    if ($matches.Count -gt 1) { throw 'Multiple releases use the requested tag.' }
    if ($matches.Count -eq 1) { return $matches[0] }
    return $null
}
function Assert-Draft($Release, [switch]$Empty) {
    if (-not $Release.draft) { throw 'An already published release must not be modified.' }
    if ($Empty -and @($Release.assets).Count -gt 0) { throw 'The existing draft is not empty; inspect it before retrying publication.' }
}
function Resolve-TagCommit($Reference) {
    $object = $Reference.object
    for ($depth = 0; $depth -lt 8 -and $object.type -eq 'tag'; $depth++) {
        $object = (Read-GitHub @('api', "repos/$repository/git/tags/$($object.sha)")).object
    }
    if ($object.type -ne 'commit') { throw 'Release tag does not resolve to a commit.' }
    return $object.sha
}

# Validate local artifacts and inspect all remote state before making any changes.
$release = Read-Release
if ($null -ne $release) { Assert-Draft $release -Empty }
$references = Read-GitHub @('api', "repos/$repository/git/matching-refs/tags/$Tag")
$reference = @($references | Where-Object { $_.ref -ceq "refs/tags/$Tag" })
if ($reference.Count -gt 1) { throw 'Multiple references use the requested tag.' }
if ($reference.Count -eq 1 -and (Resolve-TagCommit $reference[0]) -ne $Commit) {
    throw 'The existing release tag points to a different commit.'
}
if ($reference.Count -eq 0) {
    $created = Read-GitHub @('api', '--method', 'POST', "repos/$repository/git/refs", '-f', "ref=refs/tags/$Tag", '-f', "sha=$Commit")
    if ($created.ref -cne "refs/tags/$Tag" -or (Resolve-TagCommit $created) -ne $Commit) { throw 'Created tag does not match the validated commit.' }
}
$title = "Hourstone Companion $($manifest.version)"
if ($null -eq $release) {
    Invoke-GitHub @('release', 'create', $Tag, '--repo', $repository, '--verify-tag', '--draft', '--target', $Commit, '--title', $title, '--notes-file', $notesPath) | Out-Null
} else {
    # An empty draft may still refer to an earlier source-only preview.
    Invoke-GitHub @('release', 'edit', $Tag, '--repo', $repository, '--verify-tag', '--draft', '--target', $Commit, '--title', $title, '--notes-file', $notesPath) | Out-Null
}
$release = Read-Release
if ($null -eq $release) { throw 'The prepared draft release is missing.' }
Assert-Draft $release -Empty
if ($release.target_commitish -ne $Commit) { throw 'Draft target does not match the validated commit.' }
$draftId = $release.id
# No --clobber: a concurrent upload fails instead of replacing a release asset.
Invoke-GitHub (@('release', 'upload', $Tag, '--repo', $repository) + $assets.ToArray()) | Out-Null
$release = Read-Release
if ($null -eq $release -or $release.id -ne $draftId) { throw 'The draft changed during asset upload.' }
Assert-Draft $release
if ($release.target_commitish -ne $Commit -or @($release.assets).Count -ne $expectedSizes.Count) { throw 'Uploaded release does not match the validated package.' }
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($asset in $release.assets) {
    if (-not $expectedSizes.ContainsKey($asset.name) -or -not $seen.Add($asset.name) -or $asset.size -ne $expectedSizes[$asset.name]) {
        throw 'Uploaded release asset is missing, unexpected or has the wrong size.'
    }
    if ($asset.PSObject.Properties['digest'] -and $asset.digest) {
        $localFile = Join-Path $releaseRoot $asset.name
        $expectedDigest = 'sha256:' + (Get-FileHash -LiteralPath $localFile -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($asset.digest -cne $expectedDigest) { throw 'Uploaded release asset checksum does not match.' }
    }
}
# Publishing is deliberately the last mutation; failures above retain a draft.
Invoke-GitHub @('release', 'edit', $Tag, '--repo', $repository, '--verify-tag', '--draft=false', '--latest') | Out-Null
Write-Output "PASS published $Tag (signed: $($manifest.signed))"
