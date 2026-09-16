"""Exercise publication gates without building, signing, or contacting GitHub."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
POWERSHELL = shutil.which("pwsh") or shutil.which("powershell")


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

    def test_public_package_requires_signing(self):
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

    def test_publisher_rejects_unsigned_manifest(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "tools").mkdir()
            (root / "artifacts/releases").mkdir(parents=True)
            shutil.copyfile(ROOT / "tools/publish-release.ps1", root / "tools/publish-release.ps1")
            (root / "artifacts/releases/release-manifest.json").write_text(
                json.dumps({"formatVersion": 1, "signed": False, "version": "0.1.0", "assets": []}), encoding="utf-8")
            result = self.run_script(root / "tools/publish-release.ps1", "-Tag", "v0.1.0", cwd=root)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("Only a validated signed package", result.stdout + result.stderr)

    def test_publisher_rejects_changed_asset_before_network(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "tools").mkdir()
            release = root / "artifacts/releases"
            release.mkdir(parents=True)
            shutil.copyfile(ROOT / "tools/publish-release.ps1", root / "tools/publish-release.ps1")
            (release / "HourstoneCompanion-win-Setup.exe").write_bytes(b"changed fixture")
            (release / "release-manifest.json").write_text(json.dumps({
                "formatVersion": 1, "signed": True, "version": "0.1.0",
                "assets": [{"file": "HourstoneCompanion-win-Setup.exe", "sha256": "0" * 64}]}), encoding="utf-8")
            result = self.run_script(root / "tools/publish-release.ps1", "-Tag", "v0.1.0", cwd=root)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("checksum mismatch", result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
