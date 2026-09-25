#Requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PlanPath,
    [string]$ExpectedPlanSha256,
    [string]$ExpectedUserSid,
    [switch]$Execute,
    [switch]$AllowTemporaryCertificateTrust
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$preparedRoot = Join-Path $repo 'artifacts/msix-validation/prepared'
function Assert-ChildPath([string]$Path, [string]$Parent) {
    $full = [IO.Path]::GetFullPath($Path); $base = [IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    if (-not $full.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) { throw "Path is outside its expected test directory: $full" }
    for ($item = $full; $item; $item = [IO.Path]::GetDirectoryName($item)) {
        if ((Test-Path -LiteralPath $item) -and ((Get-Item -LiteralPath $item -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Reparse point is not allowed: $item" }
    }
    return $full
}
$PlanPath = Assert-ChildPath (Resolve-Path -LiteralPath $PlanPath).Path $preparedRoot
$planHash = (Get-FileHash -LiteralPath $PlanPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($ExpectedPlanSha256 -and $planHash -ne $ExpectedPlanSha256.ToLowerInvariant()) { throw 'The reviewed test plan has changed.' }
if ($Execute -and -not $ExpectedPlanSha256) { throw 'Execution requires the reviewed plan SHA256.' }
if ($Execute -and ([string]::IsNullOrWhiteSpace($ExpectedUserSid) -or [Security.Principal.WindowsIdentity]::GetCurrent().User.Value -ne $ExpectedUserSid)) { throw 'Execution requires the original Windows user SID. Alternate administrator credentials must not install or test this package for another user.' }
$plan = Get-Content -LiteralPath $PlanPath -Raw | ConvertFrom-Json
if ($plan.formatVersion -ne 1 -or $plan.identity -ne 'HourstoneCompanion.LocalTest' -or $plan.publisher -ne 'CN=HourstoneCompanion.LocalTest' -or $plan.applicationId -ne 'HourstoneCompanion' -or $plan.trustStore -ne 'LocalMachine/TrustedPeople') { throw 'Unexpected test identity or trust scope.' }
if ($plan.packages.Count -ne 2 -or $plan.packages[0].version -ne '1.0.0.0' -or $plan.packages[1].version -ne '1.0.1.0') { throw 'Unexpected test version sequence.' }
$planDirectory = Split-Path -Parent $PlanPath
function Plan-File([string]$Name) {
    if ([IO.Path]::GetFileName($Name) -ne $Name) { throw 'Test plan file names must be leaf names.' }
    return Assert-ChildPath (Join-Path $planDirectory $Name) $planDirectory
}
$certificatePath = Plan-File $plan.certificateFile
if ((Get-FileHash -LiteralPath $certificatePath -Algorithm SHA256).Hash -ne $plan.certificateSha256) { throw 'Certificate hash mismatch.' }
$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
$eku = $certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' }
$usage = $certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.15' }
$constraints = $certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.19' }
if ($certificate.Thumbprint -ne $plan.thumbprint -or $certificate.Subject -ne $plan.publisher -or $certificate.Issuer -ne $plan.publisher -or $certificate.HasPrivateKey -or
    $certificate.NotBefore -gt [DateTime]::Now -or $certificate.NotAfter -lt [DateTime]::Now -or
    $null -eq $eku -or @($eku.EnhancedKeyUsages).Count -ne 1 -or $eku.EnhancedKeyUsages[0].Value -ne '1.3.6.1.5.5.7.3.3' -or
    $null -eq $usage -or $usage.KeyUsages -ne [Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature -or
    $null -eq $constraints -or $constraints.CertificateAuthority) { throw 'Certificate is not the reviewed, currently valid, public-only code-signing end-entity certificate.' }
Add-Type -AssemblyName System.IO.Compression
foreach ($entry in $plan.packages) {
    $path = Plan-File $entry.file
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) { throw "MSIX hash mismatch: $($entry.file)" }
    $signature = Get-AuthenticodeSignature -LiteralPath $path
    if ($signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint -or $signature.Status -notin @('Valid','UnknownError','NotTrusted')) { throw 'Package signer or signature does not match the plan.' }
    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $manifestEntry = $zip.GetEntry('AppxManifest.xml')
        if ($null -eq $manifestEntry -or $manifestEntry.Length -gt 1048576) { throw 'Invalid package manifest.' }
        $reader = [IO.StreamReader]::new($manifestEntry.Open())
        try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($manifest.Package.Identity.Name -ne $plan.identity -or $manifest.Package.Identity.Publisher -ne $plan.publisher -or $manifest.Package.Identity.Version -ne $entry.version -or $manifest.Package.Identity.ProcessorArchitecture -ne 'x64') { throw 'The package manifest differs from the reviewed plan.' }
    } finally { $zip.Dispose() }
}
function Read-LegacyStartup {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run')
    try {
        if ($null -eq $key -or 'HourstoneCompanion' -notin $key.GetValueNames()) { return [ordered]@{ exists=$false; value=$null; kind=$null } }
        return [ordered]@{ exists=$true; value=$key.GetValue('HourstoneCompanion', $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames); kind=$key.GetValueKind('HourstoneCompanion').ToString() }
    } finally { if ($key) { $key.Dispose() } }
}
$existing = @(Get-AppxPackage -Name $plan.identity)
$existingData = @(Get-ChildItem -LiteralPath (Join-Path $env:LOCALAPPDATA 'Packages') -Directory -Filter 'HourstoneCompanion.LocalTest_*' -ErrorAction SilentlyContinue)
$certificateStorePath = 'Cert:\LocalMachine\TrustedPeople\' + $certificate.Thumbprint
$certificateWasTrusted = Test-Path -LiteralPath $certificateStorePath
$legacyStartup = Read-LegacyStartup
$run = Join-Path $planDirectory ('results/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($run)
$state = [ordered]@{
    planPath=$PlanPath; planSha256=$planHash; userSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    certificateThumbprint=$certificate.Thumbprint; certificateStore=$certificateStorePath; certificateWasTrusted=$certificateWasTrusted
    existingPackages=@($existing | Select-Object Name,PackageFullName,InstallLocation); existingData=@($existingData | Select-Object -ExpandProperty FullName)
    legacyStartup=$legacyStartup; certificateAdded=$false; installAttempted=$false; packageFullName=$null; packageFamilyName=$null
    dataDirectory=$null; activatedProcessId=$null; appIntegrityRids=@(); preflightPassed=$true; executed=$false; installed=$false; started=$false; upgraded=$false; profilePreserved=$false; startedAfterUpdate=$false
    packageRemoved=$false; temporaryTrustRemoved=$false; legacyStartupUnchanged=$false; cleanupErrors=@(); error=$null
    wackPassed=$false; startupTaskTested=$false; guidedImportTested=$false; passed=$false
}
function Save-State { $state | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'native-test-result.json') -Encoding utf8NoBOM }
$state | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $run 'state-before.json') -Encoding utf8NoBOM
Save-State
if (-not $Execute) {
    Write-Output "Preflight passed. Plan SHA256: $planHash"
    Write-Output "Certificate: $($certificate.Thumbprint); LocalMachine/TrustedPeople; currently present: $certificateWasTrusted"
    Write-Output "Existing test packages: $($existing.Count); residual test profile directories: $($existingData.Count)"
    Write-Output "Review: $run. No installation or certificate trust change performed."
    $certificate.Dispose(); return
}
if ($existing.Count -or $existingData.Count) { throw 'A LocalTest installation or residual profile already exists. Use a clean test user/VM; existing data will not be replaced.' }
if (-not $certificateWasTrusted) {
    if (-not $AllowTemporaryCertificateTrust) { throw 'The test certificate is not trusted. Explicit authorization for temporary LocalMachine/TrustedPeople trust is required.' }
    $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Only the temporary LocalMachine certificate trust step requires administrator rights. No automatic elevation or alternative trust store will be attempted.' }
}
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace HourstoneNativeValidation {
  [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
  public class ApplicationActivationManager { }
  [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  public interface IApplicationActivationManager {
    [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId, [MarshalAs(UnmanagedType.LPWStr)] string arguments, uint options, out uint processId);
    [PreserveSig] int ActivateForFile(IntPtr appUserModelId, IntPtr itemArray, IntPtr verb, out uint processId);
    [PreserveSig] int ActivateForProtocol(IntPtr appUserModelId, IntPtr itemArray, out uint processId);
  }
  public static class Activation {
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint access, bool inherit, uint id);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("advapi32.dll", SetLastError=true)] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError=true)] static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info, int length, out int needed);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
    public static uint Start(string appId, string args) {
      var manager = (IApplicationActivationManager)new ApplicationActivationManager();
      try { uint processId; Marshal.ThrowExceptionForHR(manager.ActivateApplication(appId,args,0,out processId)); return processId; }
      finally { Marshal.ReleaseComObject(manager); }
    }
    public static int IntegrityRid(uint processId) {
      IntPtr process=OpenProcess(0x1000,false,processId), token=IntPtr.Zero, info=IntPtr.Zero;
      if(process==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
      try {
        if(!OpenProcessToken(process,8,out token)) throw new Win32Exception(Marshal.GetLastWin32Error());
        int needed; GetTokenInformation(token,25,IntPtr.Zero,0,out needed);
        if(needed<=0) throw new Win32Exception(Marshal.GetLastWin32Error());
        info=Marshal.AllocHGlobal(needed);
        if(!GetTokenInformation(token,25,info,needed,out needed)) throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr sid=Marshal.ReadIntPtr(info); byte count=Marshal.ReadByte(GetSidSubAuthorityCount(sid));
        return Marshal.ReadInt32(GetSidSubAuthority(sid,(uint)(count-1)));
      } finally { if(info!=IntPtr.Zero) Marshal.FreeHGlobal(info); if(token!=IntPtr.Zero) CloseHandle(token); CloseHandle(process); }
    }
  }
}
'@
function Get-TestPackage([string]$Version) {
    $packages = @(Get-AppxPackage -Name $plan.identity)
    if ($packages.Count -ne 1 -or $packages[0].Publisher -ne $plan.publisher -or $packages[0].Version.ToString() -ne $Version) { throw 'Installed package identity/version is not the expected test package.' }
    if ((Get-FileHash -LiteralPath (Join-Path $packages[0].InstallLocation 'Hourstone.Companion.dll') -Algorithm SHA256).Hash -ne $plan.applicationSha256) { throw 'Installed application differs from the reviewed payload.' }
    return $packages[0]
}
$activatedExecutable = $null
function Invoke-IsolatedStartup($Package, [string]$Phase) {
    $script:activatedExecutable = Join-Path $Package.InstallLocation 'Hourstone.Companion.exe'
    $report = Join-Path $state.dataDirectory 'smoke-result.json'
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
    $state.activatedProcessId = [HourstoneNativeValidation.Activation]::Start(($Package.PackageFamilyName + '!' + $plan.applicationId), ('--smoke-test --data-directory "' + $state.dataDirectory + '"'))
    Save-State
    $integrity = [HourstoneNativeValidation.Activation]::IntegrityRid($state.activatedProcessId)
    $state.appIntegrityRids += $integrity
    if ($integrity -lt 0x2000 -or $integrity -ge 0x3000) { throw 'The activated application is not running at normal medium integrity; this run cannot validate ordinary user behavior.' }
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    do {
        Start-Sleep -Milliseconds 200
        $process = Get-Process -Id $state.activatedProcessId -ErrorAction SilentlyContinue
    } while ($process -and [DateTime]::UtcNow -lt $deadline)
    if ($process) { throw 'Packaged application did not complete its isolated startup within 45 seconds.' }
    if (-not (Test-Path -LiteralPath $report)) { throw 'The activated package did not create its startup report.' }
    $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
    if (-not $result.passed -or $result.distribution -ne 'Store' -or -not $result.databaseCreated -or -not $result.windowVisible -or -not $result.dispatcherRunning -or $result.dataDirectory -ne $state.dataDirectory) { throw 'Packaged runtime or isolated startup verification failed.' }
    Copy-Item -LiteralPath $report -Destination (Join-Path $run "$Phase-startup.json")
    $state.activatedProcessId = $null
}
function Profile-Hashes {
    return @(Get-ChildItem -LiteralPath $state.dataDirectory -File -Recurse | Sort-Object FullName | ForEach-Object {
        Assert-ChildPath $_.FullName $state.dataDirectory | Out-Null
        '{0} {1}' -f $_.FullName.Substring($state.dataDirectory.Length),(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    })
}
$failure = $null
try {
    $state.executed = $true
    if (-not $certificateWasTrusted) {
        $state.certificateAdded = $true; Save-State
        $store = [Security.Cryptography.X509Certificates.X509Store]::new('TrustedPeople','LocalMachine')
        try { $store.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite); $store.Add($certificate) } finally { $store.Dispose() }
    }
    foreach ($entry in $plan.packages) { if ((Get-AuthenticodeSignature -LiteralPath (Plan-File $entry.file)).Status -ne 'Valid') { throw 'Package signature is not valid after the explicit trust step.' } }
    $state.installAttempted = $true; Save-State
    Add-AppxPackage -Path (Plan-File $plan.packages[0].file) -ErrorAction Stop
    $package = Get-TestPackage $plan.packages[0].version
    $state.installed = $true; $state.packageFullName = $package.PackageFullName; $state.packageFamilyName = $package.PackageFamilyName
    $packageData = Join-Path $env:LOCALAPPDATA ('Packages/' + $package.PackageFamilyName)
    $state.dataDirectory = Assert-ChildPath (Join-Path $packageData 'LocalState/validation-smoke') $packageData
    Save-State
    Invoke-IsolatedStartup $package 'initial'; $state.started = $true
    [IO.File]::WriteAllText((Join-Path $state.dataDirectory 'upgrade-sentinel.txt'), [Guid]::NewGuid().ToString('N'))
    $beforeUpdate = Profile-Hashes
    Copy-Item -LiteralPath $state.dataDirectory -Destination (Join-Path $run 'profile-backup') -Recurse
    $beforeUpdate | Set-Content -LiteralPath (Join-Path $run 'profile-before-update.sha256')
    Add-AppxPackage -Path (Plan-File $plan.packages[1].file) -ErrorAction Stop
    $package = Get-TestPackage $plan.packages[1].version
    $state.upgraded = $true; $state.packageFullName = $package.PackageFullName
    if (($beforeUpdate -join "`n") -ne ((Profile-Hashes) -join "`n")) { throw 'The MSIX update changed or lost the isolated profile.' }
    $state.profilePreserved = $true; Save-State
    Invoke-IsolatedStartup $package 'updated'; $state.startedAfterUpdate = $true
} catch { $failure = $_; $state.error = $_.Exception.Message }
finally {
    if ($state.activatedProcessId) {
        try {
            $process = Get-Process -Id $state.activatedProcessId -ErrorAction SilentlyContinue
            if ($process -and $process.Path -eq $activatedExecutable) { Stop-Process -Id $process.Id -ErrorAction Stop }
        } catch { $state.cleanupErrors += $_.Exception.Message }
    }
    if ($state.installAttempted) {
        try {
            $installed = @(Get-AppxPackage -Name $plan.identity)
            foreach ($package in $installed) {
                if ($package.Publisher -ne $plan.publisher -or $package.Version.ToString() -notin $plan.packages.version) { throw 'Cleanup refused an unexpected package identity/version.' }
                Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop
            }
            $state.packageRemoved = @(Get-AppxPackage -Name $plan.identity).Count -eq 0
        } catch { $state.cleanupErrors += $_.Exception.Message }
    }
    if ($state.certificateAdded) {
        try {
            if (Test-Path -LiteralPath $certificateStorePath) { Remove-Item -LiteralPath $certificateStorePath -ErrorAction Stop }
            $state.temporaryTrustRemoved = -not (Test-Path -LiteralPath $certificateStorePath)
        } catch { $state.cleanupErrors += $_.Exception.Message }
    } else { $state.temporaryTrustRemoved = $true }
    try { $state.legacyStartupUnchanged = (ConvertTo-Json -Compress $legacyStartup) -eq (ConvertTo-Json -Compress (Read-LegacyStartup)) }
    catch { $state.cleanupErrors += $_.Exception.Message }
    $state.passed = $state.installed -and $state.started -and $state.upgraded -and $state.profilePreserved -and $state.startedAfterUpdate -and $state.packageRemoved -and $state.temporaryTrustRemoved -and $state.legacyStartupUnchanged -and $state.cleanupErrors.Count -eq 0 -and $null -eq $failure
    Save-State; $certificate.Dispose()
    Write-Output "Native test result: $(Join-Path $run 'native-test-result.json')"
}
if ($failure) { throw $failure }
if (-not $state.passed) { throw 'Native test or cleanup did not pass. Review the result and state-before files; no Windows security policy was altered.' }
Write-Output 'PASS install, packaged startup, upgrade with preserved private profile, second startup, package removal, and temporary trust cleanup. WACK, autostart and guided import remain separate tests.'
