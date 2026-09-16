"""Exercise publication gates and draft transitions without contacting GitHub."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
POWERSHELL = shutil.which("pwsh") or shutil.which("powershell")
COMMIT = "1" * 40
ASSETS = (
    "HourstoneCompanion-0.1.0-full.nupkg", "HourstoneCompanion-win-Portable.zip",
    "HourstoneCompanion-win-Setup.exe", "releases.win.json", "SHA256SUMS",
)
LOCAL_EVIDENCE = (
    "RELEASES", "assets.win.json",
    "dependencies.cdx.json", "DEPENDENCY-NOTICES.txt", "ASSET-NOTICES.txt",
    "assets-manifest.json", "app-icon.json", "LICENSE.txt",
)

STUB = r'''
param([string]$Scenario, [string]$Tag = 'v0.1.0', [string]$Commit, [switch]$AllowUnsigned)
$ErrorActionPreference = 'Stop'
$global:releaseFixtureCalls = [Collections.Generic.List[object]]::new()
$global:releaseFixtureReference = $null
$global:releaseFixtureRelease = if ($Scenario -eq 'new') { $null } else {
    @{ id = 123; tag_name = 'v0.1.0'; draft = $Scenario -ne 'published'; target_commitish = ('2' * 40); assets = @() }
}
if ($Scenario -eq 'nonempty') { $global:releaseFixtureRelease.assets = @(@{ name = 'existing.zip'; size = 4 }) }
if ($Scenario -eq 'wrong-tag') { $global:releaseFixtureReference = @{ ref = 'refs/tags/v0.1.0'; object = @{ type = 'commit'; sha = ('2' * 40) } } }
if ($Scenario -eq 'existing-tag') { $global:releaseFixtureReference = @{ ref = 'refs/tags/v0.1.0'; object = @{ type = 'commit'; sha = ('1' * 40) } } }
function git { $global:LASTEXITCODE = 0; return ('1' * 40) }
function Get-AuthenticodeSignature { return @{ Status = 'NotSigned' } }
function gh {
    $arguments = @($args)
    $global:releaseFixtureCalls.Add($arguments)
    $global:LASTEXITCODE = 0
    if ($arguments[0] -eq 'api') {
        $endpoint = @($arguments | Where-Object { $_ -like 'repos/*' })[0]
        if ($Scenario -eq 'api-failure') { $global:LASTEXITCODE = 1; return }
        if ($endpoint -like '*/releases?*') {
            $page = if ($null -eq $global:releaseFixtureRelease) { '[]' } else { ConvertTo-Json -InputObject @($global:releaseFixtureRelease) -Depth 10 -Compress }
            return '[' + $page + ']'
        }
        if ($endpoint -like '*/git/matching-refs/*') {
            if ($null -eq $global:releaseFixtureReference) { return '[]' }
            return ConvertTo-Json -InputObject @($global:releaseFixtureReference) -Depth 10 -Compress
        }
        if ($endpoint -like '*/git/refs' -and $arguments -contains 'POST') {
            $sha = @($arguments | Where-Object { $_ -like 'sha=*' })[0].Substring(4)
            $global:releaseFixtureReference = @{ ref = 'refs/tags/v0.1.0'; object = @{ type = 'commit'; sha = $sha } }
            return ConvertTo-Json $global:releaseFixtureReference -Depth 10 -Compress
        }
        throw 'Unexpected API fixture request.'
    }
    if ($arguments[0] -eq 'release') {
        if ($arguments[1] -eq 'create' -or ($arguments[1] -eq 'edit' -and $arguments -contains '--draft')) {
            if ($arguments -notcontains '--draft' -or $arguments -notcontains '--verify-tag') { throw 'Only a draft may be prepared.' }
            $targetIndex = [Array]::IndexOf($arguments, '--target')
            $global:releaseFixtureRelease = @{ id = 123; tag_name = 'v0.1.0'; draft = $true; target_commitish = $arguments[$targetIndex + 1]; assets = @() }
            return 'prepared'
        }
        if ($arguments[1] -eq 'upload') {
            if ($arguments -contains '--clobber') { throw 'Clobber must never be used.' }
            if ($Scenario -eq 'upload-failure') { $global:LASTEXITCODE = 1; return }
            $global:releaseFixtureRelease.assets = @($arguments | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | ForEach-Object {
                $file = Get-Item -LiteralPath $_
                @{ name = $file.Name; size = $file.Length; digest = 'sha256:' + (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() }
            })
            if ($Scenario -eq 'wrong-size') { $global:releaseFixtureRelease.assets[0].size++ }
            if ($Scenario -eq 'wrong-digest') { $global:releaseFixtureRelease.assets[0].digest = 'sha256:' + ('0' * 64) }
            if ($Scenario -eq 'missing-upload') { $global:releaseFixtureRelease.assets = @($global:releaseFixtureRelease.assets | Select-Object -Skip 1) }
            if ($Scenario -eq 'draft-replaced') { $global:releaseFixtureRelease.id = 456 }
            return 'uploaded'
        }
        if ($arguments[1] -eq 'edit' -and $arguments -contains '--draft=false') {
            if (-not $global:releaseFixtureRelease.draft) { throw 'Published release must not be edited.' }
            $global:releaseFixtureRelease.draft = $false
            return 'published'
        }
    }
    throw 'Unexpected GitHub fixture request.'
}
try {
    $options = @{ Tag = $Tag; Commit = $Commit; AllowUnsigned = $AllowUnsigned }
    & (Join-Path $PSScriptRoot 'tools/publish-release.ps1') @options
} finally {
    ConvertTo-Json -InputObject $global:releaseFixtureCalls.ToArray() -Depth 10 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'calls.json')
}
'''


@unittest.skipUnless(POWERSHELL, "PowerShell is required for Windows packaging checks")
class PackagingGates(unittest.TestCase):
    def run_script(self, relative, *arguments, env=None, cwd=None):
        task_env = os.environ.copy()
        for key in list(task_env):
            if key.startswith("SIGNING_CERTIFICATE_") or key.startswith("GITHUB_REF"):
                task_env.pop(key)
        task_env.update(env or {})
        return subprocess.run([POWERSHELL, "-NoProfile", "-File", str(relative), *arguments],
                              cwd=cwd or ROOT, env=task_env, capture_output=True, text=True,
                              encoding="utf-8", errors="replace")

    def test_public_package_requires_signing_by_default(self):
        result = self.run_script(ROOT / "tools/package.ps1")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Public packaging requires a signing certificate", result.stdout + result.stderr)

    def test_release_tag_rejects_unsigned_package(self):
        version = __import__("xml.etree.ElementTree", fromlist=["parse"]).parse(ROOT / "Directory.Build.props").getroot().find(".//Version").text
        result = self.run_script(ROOT / "tools/package.ps1", "-Unsigned",
                                 env={"GITHUB_REF_TYPE": "tag", "GITHUB_REF_NAME": f"v{version}"})
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Unsigned packages cannot", result.stdout + result.stderr)

    def test_incorrect_version_stops_before_build(self):
        result = self.run_script(ROOT / "tools/package.ps1", "-Unsigned", "-Version", "999.0.0")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Package version must match", result.stdout + result.stderr)

    def fixture(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        root = Path(temp.name)
        (root / "tools").mkdir()
        (root / "docs").mkdir()
        (root / "Directory.Build.props").write_text("<Project><PropertyGroup><Version>0.1.0</Version></PropertyGroup></Project>", encoding="utf-8")
        (root / "docs/RELEASE-NOTES.md").write_text("Synthetic release notes\n", encoding="utf-8")
        release = root / "artifacts/releases"
        release.mkdir(parents=True)
        shutil.copyfile(ROOT / "tools/publish-release.ps1", root / "tools/publish-release.ps1")
        shutil.copyfile(ROOT / "tools/release-assets.ps1", root / "tools/release-assets.ps1")
        (root / "fixture.ps1").write_text(STUB, encoding="utf-8")
        entries = []
        for name in ASSETS[:-1]:
            data = ("Synthetic package fixture: " + name).encode()
            (release / name).write_bytes(data)
            entries.append({"file": name, "sha256": hashlib.sha256(data).hexdigest()})
        checksum_data = ("\n".join(f"{entry['sha256']}  {entry['file']}" for entry in entries) + "\n").encode()
        (release / "SHA256SUMS").write_bytes(checksum_data)
        entries.append({"file": "SHA256SUMS", "sha256": hashlib.sha256(checksum_data).hexdigest()})
        for name in LOCAL_EVIDENCE:
            (release / name).write_text("Local evidence fixture: " + name, encoding="utf-8")
        manifest = {"formatVersion": 1, "signed": False, "runtime": "win-x64", "version": "0.1.0", "assets": entries}
        self.write_manifest(root, manifest)
        return root, manifest

    def write_manifest(self, root, manifest):
        (root / "artifacts/releases/release-manifest.json").write_text(json.dumps(manifest), encoding="utf-8")

    def publish(self, root, scenario="empty", allow=True, commit=COMMIT, tag="v0.1.0"):
        options = ["-Scenario", scenario, "-Tag", tag, "-Commit", commit]
        if allow:
            options.append("-AllowUnsigned")
        result = self.run_script(root / "fixture.ps1", *options, cwd=root)
        calls = json.loads((root / "calls.json").read_text(encoding="utf-8-sig"))
        return result, calls

    def assert_rejected(self, result, message):
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(message, result.stdout + result.stderr)

    def test_unsigned_publisher_requires_explicit_opt_in(self):
        root, _ = self.fixture()
        result, calls = self.publish(root, allow=False)
        self.assert_rejected(result, "Explicit -AllowUnsigned is required")
        self.assertEqual(calls, [])

    def test_unsigned_opt_in_never_bypasses_a_claimed_signature(self):
        root, manifest = self.fixture()
        manifest["signed"] = True
        self.write_manifest(root, manifest)
        result, calls = self.publish(root)
        self.assert_rejected(result, "Installer signature is not valid")
        self.assertEqual(calls, [])

    def test_changed_asset_is_rejected_before_network(self):
        root, _ = self.fixture()
        (root / "artifacts/releases/HourstoneCompanion-win-Setup.exe").write_bytes(b"changed fixture")
        result, calls = self.publish(root)
        self.assert_rejected(result, "checksum mismatch")
        self.assertEqual(calls, [])

    def test_incomplete_duplicate_or_unexpected_asset_list_is_rejected(self):
        for change, message in (("missing", "complete installer"), ("duplicate", "Duplicate release asset"), ("unexpected", "Unexpected release asset")):
            with self.subTest(change=change):
                root, manifest = self.fixture()
                if change == "missing":
                    manifest["assets"] = [entry for entry in manifest["assets"] if entry["file"] != "releases.win.json"]
                elif change == "duplicate":
                    manifest["assets"].append(manifest["assets"][0].copy())
                else:
                    manifest["assets"][0]["file"] = "../outside.zip"
                self.write_manifest(root, manifest)
                result, calls = self.publish(root)
                self.assert_rejected(result, message)
                self.assertEqual(calls, [])

    def test_tag_and_commit_must_match_the_validated_checkout(self):
        for arguments, message in (({"tag": "v9.9.9"}, "matching stable version tag"), ({"commit": "2" * 40}, "checked-out commit")):
            with self.subTest(arguments=arguments):
                root, _ = self.fixture()
                result, calls = self.publish(root, **arguments)
                self.assert_rejected(result, message)
                self.assertEqual(calls, [])

    def test_published_nonempty_or_wrong_tag_release_is_never_modified(self):
        for scenario, message in (("published", "already published"), ("nonempty", "draft is not empty"), ("wrong-tag", "different commit"), ("api-failure", "GitHub release operation failed")):
            with self.subTest(scenario=scenario):
                root, _ = self.fixture()
                result, calls = self.publish(root, scenario=scenario)
                self.assert_rejected(result, message)
                self.assertTrue(all(call[0] == "api" and "POST" not in call for call in calls))

    def test_empty_draft_or_new_release_is_published_only_after_asset_verification(self):
        for scenario in ("empty", "new", "existing-tag"):
            with self.subTest(scenario=scenario):
                root, _ = self.fixture()
                result, calls = self.publish(root, scenario=scenario)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                tag_creates = [call for call in calls if call[0] == "api" and "POST" in call]
                self.assertEqual(len(tag_creates), 0 if scenario == "existing-tag" else 1)
                prepare = next(call for call in calls if call[0] == "release" and "--draft" in call)
                self.assertEqual(prepare[1], "create" if scenario == "new" else "edit")
                self.assertEqual(prepare[prepare.index("--target") + 1], COMMIT)
                upload = next(call for call in calls if call[:2] == ["release", "upload"])
                self.assertNotIn("--clobber", upload)
                uploaded_names = {Path(item).name for item in upload if Path(item).is_file()}
                self.assertEqual(uploaded_names, set(ASSETS))
                self.assertNotIn("release-manifest.json", uploaded_names)
                self.assertTrue(uploaded_names.isdisjoint(LOCAL_EVIDENCE))
                self.assertEqual(calls[-1][:2], ["release", "edit"])
                self.assertIn("--draft=false", calls[-1])
                self.assertIn("--latest", calls[-1])
                self.assertEqual(calls[-2][0], "api")

    def test_failed_upload_or_remote_verification_keeps_release_unpublished(self):
        for scenario, message in (("upload-failure", "GitHub release operation failed"), ("wrong-size", "wrong size"), ("wrong-digest", "checksum does not match"), ("missing-upload", "does not match the validated package"), ("draft-replaced", "draft changed")):
            with self.subTest(scenario=scenario):
                root, _ = self.fixture()
                result, calls = self.publish(root, scenario=scenario)
                self.assert_rejected(result, message)
                self.assertFalse(any("--draft=false" in call for call in calls))

    def test_checksums_must_describe_exactly_the_four_other_public_assets(self):
        for change, message in (("extra", "exactly the four"), ("duplicate", "unexpected or duplicate"), ("wrong-hash", "does not match"), ("self", "unexpected or duplicate")):
            with self.subTest(change=change):
                root, manifest = self.fixture()
                checksum_file = root / "artifacts/releases/SHA256SUMS"
                lines = checksum_file.read_text(encoding="utf-8").splitlines()
                if change == "extra":
                    lines.append("0" * 64 + "  LICENSE.txt")
                elif change == "duplicate":
                    lines[-1] = lines[0]
                elif change == "wrong-hash":
                    lines[0] = "0" * 64 + lines[0][64:]
                else:
                    lines[0] = "0" * 64 + "  SHA256SUMS"
                checksum_file.write_text("\n".join(lines) + "\n", encoding="utf-8")
                next(entry for entry in manifest["assets"] if entry["file"] == "SHA256SUMS")["sha256"] = hashlib.sha256(checksum_file.read_bytes()).hexdigest()
                self.write_manifest(root, manifest)
                result, calls = self.publish(root)
                self.assert_rejected(result, message)
                self.assertEqual(calls, [])

    def test_local_evidence_cannot_be_added_to_public_manifest(self):
        root, manifest = self.fixture()
        evidence = root / "artifacts/releases/LICENSE.txt"
        manifest["assets"].append({"file": evidence.name, "sha256": hashlib.sha256(evidence.read_bytes()).hexdigest()})
        self.write_manifest(root, manifest)
        result, calls = self.publish(root)
        self.assert_rejected(result, "Unexpected release asset")
        self.assertEqual(calls, [])

    def test_packaging_manifest_selects_only_public_files_and_preserves_evidence(self):
        root, _ = self.fixture()
        release = root / "artifacts/releases"
        evidence_before = {name: (release / name).read_bytes() for name in LOCAL_EVIDENCE}
        (release / "HourstoneCompanion-9.9.9-full.nupkg").write_bytes(b"Other version fixture")
        (release / "SHA256SUMS").write_text("Previous expanded checksum list", encoding="utf-8")
        (release / "release-manifest.json").write_text("Previous expanded manifest", encoding="utf-8")
        wrapper = root / "manifest-fixture.ps1"
        wrapper.write_text("""$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'tools/release-assets.ps1')
Write-PublicReleaseManifest -ReleaseDirectory (Join-Path $PSScriptRoot 'artifacts/releases') -Version '0.1.0' -Signed $false
""", encoding="utf-8")
        result = self.run_script(wrapper, cwd=root)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        manifest = json.loads((release / "release-manifest.json").read_text(encoding="utf-8"))
        self.assertEqual({entry["file"] for entry in manifest["assets"]}, set(ASSETS))
        self.assertEqual(len(manifest["assets"]), 5)
        self.assertEqual((manifest["formatVersion"], manifest["version"], manifest["runtime"], manifest["signed"]), (1, "0.1.0", "win-x64", False))
        for entry in manifest["assets"]:
            self.assertEqual(entry["sha256"], hashlib.sha256((release / entry["file"]).read_bytes()).hexdigest())
        checksums = (release / "SHA256SUMS").read_text(encoding="utf-8").splitlines()
        expected = {f"{hashlib.sha256((release / name).read_bytes()).hexdigest()}  {name}" for name in ASSETS[:-1]}
        self.assertEqual(set(checksums), expected)
        self.assertEqual(len(checksums), 4)
        self.assertEqual(evidence_before, {name: (release / name).read_bytes() for name in LOCAL_EVIDENCE})
        self.assertTrue((release / "HourstoneCompanion-9.9.9-full.nupkg").is_file())
        result, calls = self.publish(root)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        uploaded = next(call for call in calls if call[:2] == ["release", "upload"])
        self.assertEqual({Path(item).name for item in uploaded if Path(item).is_file()}, set(ASSETS))


if __name__ == "__main__":
    unittest.main()
