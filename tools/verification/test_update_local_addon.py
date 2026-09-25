"""Exercise the PowerShell updater only against disposable synthetic clients.

No real installation, process, account database or LocalAppData backup is touched.
PowerShell functions are dot-sourced; process enumeration and the backup root are
replaced by test-local functions, without a production bypass switch.
"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "tools/update-local-addon.ps1"
PWSH = shutil.which("pwsh")
TOC = "## Version: 0.3.2\n## X-Hourstone-Sync-Protocol: 4\nCore.lua\n"


@unittest.skipUnless(os.name == "nt" and PWSH, "Windows and PowerShell 7 required")
class LocalAddonUpdateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="Hourstone.AddonUpdater.Tests-")
        self.root = Path(self.temp.name)
        self.client = self.root / "WoW" / "_retail_"
        self.target = self.client / "Interface/AddOns/Hourstone"
        self.target.mkdir(parents=True)
        (self.client / "Wow.exe").write_bytes(b"synthetic executable marker; never run")
        (self.target / "Hourstone.toc").write_text("## Version: 0.3.1\nOld.lua\n", encoding="utf-8")
        (self.target / "Old.lua").write_text("-- old synthetic addon\n", encoding="utf-8")
        self.saved = self.client / "WTF/Account/SYNTHETIC/SavedVariables/Hourstone.lua"
        self.saved.parent.mkdir(parents=True)
        self.saved.write_text("untouched synthetic game-data canary", encoding="utf-8")
        self.backups = self.root / "LocalAppData/Hourstone/Backups/Addon"
        self.archive = self.root / "addon.zip"
        self.write_archive()
        self.wrapper = self.root / "invoke.ps1"
        self.wrapper.write_text(r'''
$ErrorActionPreference='Stop'
. $env:HOURSTONE_ADDON_TEST_SCRIPT
$global:tampered=$false
function Get-AddonBackupRoot { $env:HOURSTONE_ADDON_TEST_BACKUPS }
function Get-CimInstance {
    param($ClassName, $Filter, $ErrorAction)
    if ($env:HOURSTONE_ADDON_TEST_PROCESS_ERROR -eq '1') { throw 'synthetic enumeration failure' }
    if ($env:HOURSTONE_ADDON_TEST_TAMPER -eq '1' -and -not $global:tampered) {
        $parent=Join-Path $env:HOURSTONE_ADDON_TEST_CLIENT 'Interface/AddOns'
        $stage=Get-ChildItem -LiteralPath $parent -Directory | Where-Object Name -Like '.Hourstone-update-*' | Select-Object -First 1
        if ($stage) {
            [IO.File]::WriteAllText((Join-Path $stage.FullName 'Core.lua'), '-- synthetic staged-file drift')
            $global:tampered=$true
        }
    }
    if ($env:HOURSTONE_ADDON_TEST_PROCESSES) { ConvertFrom-Json -InputObject $env:HOURSTONE_ADDON_TEST_PROCESSES }
}
if ($env:HOURSTONE_ADDON_TEST_JUNCTION -eq '1') {
    $outside=Join-Path $env:HOURSTONE_ADDON_TEST_ROOT 'outside'
    New-Item -ItemType Directory -Path $outside | Out-Null
    New-Item -ItemType Junction -Path (Join-Path $env:HOURSTONE_ADDON_TEST_CLIENT 'Interface/AddOns/Hourstone/Media') -Target $outside | Out-Null
}
Invoke-LocalAddonUpdate -ClientDirectory @($env:HOURSTONE_ADDON_TEST_CLIENT) -ArchivePath $env:HOURSTONE_ADDON_TEST_ARCHIVE -ExpectedSha256 $env:HOURSTONE_ADDON_TEST_SHA -ExpectedVersion '0.3.2' -Apply:($env:HOURSTONE_ADDON_TEST_APPLY -eq '1')
''', encoding="utf-8")

    def tearDown(self):
        # TemporaryDirectory does not traverse directory junction targets.
        self.temp.cleanup()

    def write_archive(self, entries=None):
        entries = entries or {"Hourstone/Hourstone.toc": TOC, "Hourstone/Core.lua": "-- new synthetic addon\n"}
        with zipfile.ZipFile(self.archive, "w", zipfile.ZIP_DEFLATED) as archive:
            for name, data in entries.items():
                entry = zipfile.ZipInfo(name)
                # ZipInfo normalizes Windows separators in its constructor;
                # preserve malformed raw names so this is a real rejection test.
                entry.filename = name
                entry.compress_type = zipfile.ZIP_DEFLATED
                archive.writestr(entry, data)

    def run_update(self, apply=False, processes=None, **options):
        env = dict(os.environ)
        values = {
            "SCRIPT": SCRIPT, "BACKUPS": self.backups, "CLIENT": self.client,
            "ARCHIVE": self.archive, "SHA": hashlib.sha256(self.archive.read_bytes()).hexdigest(),
            "PROCESSES": json.dumps(processes or []), "APPLY": "1" if apply else "0", "ROOT": self.root,
            **options,
        }
        env.update({"HOURSTONE_ADDON_TEST_" + key: str(value) for key, value in values.items()})
        result = subprocess.run([PWSH, "-NoProfile", "-File", str(self.wrapper)], env=env, capture_output=True, text=True, timeout=45)
        self.assertEqual(self.saved.read_text(encoding="utf-8"), "untouched synthetic game-data canary")
        self.assertNotIn("SYNTHETIC", result.stdout)
        self.assertNotIn("game-data canary", result.stdout + result.stderr)
        return result

    def assert_original(self):
        self.assertTrue((self.target / "Old.lua").is_file())
        self.assertFalse((self.target / "Core.lua").exists())

    def test_dry_run_does_not_write_and_reports_reviewable_plan(self):
        result = self.run_update()
        self.assertEqual(result.returncode, 0, result.stderr)
        report = json.loads(result.stdout)
        self.assertEqual(report["Mode"], "DryRun")
        self.assertTrue(report["CanApply"])
        self.assertEqual(report["Targets"][0]["ExistingVersion"], "0.3.1")
        self.assertEqual(report["FileCount"], 2)
        self.assertFalse(self.backups.exists())
        self.assert_original()

    def test_real_packaged_archive_preflight_uses_only_synthetic_target(self):
        self.archive = ROOT / "artifacts/addon/Hourstone-0.3.2.zip"
        if not self.archive.exists():
            self.skipTest("Local addon artifact is not present")
        result = self.run_update()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(json.loads(result.stdout)["FileCount"], 45)
        self.assertFalse(self.backups.exists())
        self.assert_original()

    def test_apply_backs_up_and_replaces_only_addon(self):
        result = self.run_update(apply=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        report = json.loads(result.stdout)
        self.assertEqual(report["Status"], "Applied")
        self.assertTrue((Path(report["Backup"]) / "Old.lua").is_file())
        self.assertEqual((self.target / "Core.lua").read_text(), "-- new synthetic addon\n")
        self.assertFalse((self.target / "Old.lua").exists())
        self.assertEqual([p.name for p in self.target.parent.iterdir()], ["Hourstone"])

    def test_target_process_and_unreadable_process_block_without_mutation(self):
        for path in (str(self.client / "Wow.exe"), None):
            with self.subTest(path=path):
                result = self.run_update(apply=True, processes=[{"Name": "Wow.exe", "ExecutablePath": path}])
                self.assertNotEqual(result.returncode, 0)
                self.assertIn('"CanApply": false', result.stdout)
                self.assertFalse(self.backups.exists())
                self.assert_original()

    def test_known_other_client_process_does_not_block_target(self):
        result = self.run_update(processes=[{"Name": "WowClassic.exe", "ExecutablePath": str(self.root / "OtherWoW/_anniversary_/WowClassic.exe")}])
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue(json.loads(result.stdout)["CanApply"])

    def test_process_inspection_failure_is_closed(self):
        result = self.run_update(apply=True, PROCESS_ERROR="1")
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse(self.backups.exists())
        self.assert_original()

    def test_hash_version_and_missing_module_are_rejected(self):
        result = self.run_update(apply=True, SHA="0" * 64)
        self.assertNotEqual(result.returncode, 0)
        for toc in (TOC.replace("0.3.2", "0.3.3"), TOC + "Missing.lua\n"):
            self.write_archive({"Hourstone/Hourstone.toc": toc, "Hourstone/Core.lua": "-- test"})
            self.assertNotEqual(self.run_update(apply=True).returncode, 0)
        self.assertFalse(self.backups.exists())
        self.assert_original()

    def test_traversal_wrong_root_windows_paths_and_collisions_rejected(self):
        for path in ("Hourstone/../outside.lua", "Other/evil.lua", "Hourstone/C:evil", "Hourstone/CON.txt", "Hourstone/a./file", "Hourstone/WTF/file", "Hourstone\\evil.lua", "Hourstone/core.lua"):
            with self.subTest(path=path):
                self.write_archive({"Hourstone/Hourstone.toc": TOC, "Hourstone/Core.lua": "-- test", path: "bad"})
                self.assertNotEqual(self.run_update(apply=True).returncode, 0)
                self.assertFalse(self.backups.exists())
                self.assert_original()

    def test_zip_symlink_is_rejected(self):
        with zipfile.ZipFile(self.archive, "a") as archive:
            link = zipfile.ZipInfo("Hourstone/Link.lua")
            link.create_system = 3
            link.external_attr = 0o120777 << 16
            archive.writestr(link, "../outside")
        self.assertNotEqual(self.run_update(apply=True).returncode, 0)
        self.assertFalse(self.backups.exists())
        self.assert_original()

    def test_existing_junction_is_rejected_without_following_it(self):
        result = self.run_update(apply=True, JUNCTION="1")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("reparse point", result.stderr)
        self.assertFalse(self.backups.exists())
        self.assert_original()

    def test_staged_file_drift_rolls_back_and_retains_verified_backup(self):
        result = self.run_update(apply=True, TAMPER="1")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("restored", result.stderr)
        self.assert_original()
        copies = list(self.backups.glob("*/Hourstone/Old.lua"))
        self.assertEqual(len(copies), 1)
        self.assertEqual(copies[0].read_bytes(), (self.target / "Old.lua").read_bytes())

    def test_newer_installed_version_is_not_downgraded(self):
        (self.target / "Hourstone.toc").write_text("## Version: 0.4.0\n", encoding="utf-8")
        self.assertNotEqual(self.run_update(apply=True).returncode, 0)
        self.assertFalse(self.backups.exists())
        self.assert_original()


if __name__ == "__main__":
    unittest.main()
