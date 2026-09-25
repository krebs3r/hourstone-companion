[CmdletBinding()]
param(
    [string]$AddonRoot = (Join-Path $PSScriptRoot '../../Hourstone – Azeroth Hours'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/roundtrip-final'),
    [string]$Dotnet = (Join-Path $env:ProgramFiles 'dotnet/dotnet.exe'),
    [string]$Python
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$AddonRoot = [IO.Path]::GetFullPath($AddonRoot)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = Join-Path $repoRoot 'artifacts'
if (-not $OutputDirectory.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputDirectory must be inside this repository artifacts directory.' }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new output directory; verification evidence is never overwritten.' }
for ($ancestor = $OutputDirectory; $ancestor; $ancestor = [IO.Path]::GetDirectoryName($ancestor)) {
    if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Output path must not contain reparse points.' }
}
if (-not $Python) { $Python = Join-Path $AddonRoot '.venv312/Scripts/python.exe' }
if (-not (Test-Path -LiteralPath $Python -PathType Leaf)) { throw 'Provide -Python with lupa.lua51 and Pillow installed (addon test dependencies).' }
if (-not (Test-Path -LiteralPath $Dotnet -PathType Leaf)) { throw 'Provide -Dotnet pointing to the .NET 10 SDK executable.' }
if (-not (Test-Path -LiteralPath (Join-Path $AddonRoot 'tests/run.py'))) { throw 'AddonRoot must be the Hourstone addon source repository with its test harness and v0.3.1 tag.' }
$harness = Join-Path $OutputDirectory 'harness'
New-Item -ItemType Directory -Path $harness | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'verify-progress-roundtrip/Program.cs') -Destination $harness
$coreProject = [Security.SecurityElement]::Escape((Join-Path $repoRoot 'src/Hourstone.Companion.Core/Hourstone.Companion.Core.csproj'))
@"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><ProjectReference Include="$coreProject" /></ItemGroup></Project>
"@ | Set-Content -LiteralPath (Join-Path $harness 'Roundtrip.csproj') -Encoding utf8
# This isolated integration harness uses the existing dependency cache; production dependency auditing is a separate release check.
& $Dotnet run --project (Join-Path $harness 'Roundtrip.csproj') --configuration Release --artifacts-path (Join-Path $OutputDirectory 'build') -p:NuGetAudit=false -- $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Companion roundtrip failed; see preserved output evidence.' }
& $Python (Join-Path $PSScriptRoot 'verify-progress-roundtrip/verify_lua.py') --addon-root $AddonRoot --output $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Actual Lua importer verification failed; see preserved output evidence.' }
Write-Output ('Report: ' + (Join-Path $OutputDirectory 'report.json'))
