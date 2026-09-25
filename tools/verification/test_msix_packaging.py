"""MSIX production intent gates run before restore, publish, or artifact deletion."""
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
POWERSHELL = shutil.which("pwsh")


class MsixManifestContract(unittest.TestCase):
    def test_desktop_package_needs_only_full_trust_and_optional_startup(self):
        manifest = ET.parse(ROOT / "packaging/AppxManifest.xml").getroot()
        ns = {"app": "http://schemas.microsoft.com/appx/manifest/foundation/windows10",
              "rescap": "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities",
              "desktop": "http://schemas.microsoft.com/appx/manifest/desktop/windows10"}
        capabilities = manifest.findall("app:Capabilities/rescap:Capability", ns)
        self.assertEqual([cap.get("Name") for cap in capabilities], ["runFullTrust"])
        self.assertFalse(any("Virtualization" in element.tag or "ExcludedKey" in element.tag for element in manifest.iter()))
        startup = manifest.find(".//desktop:StartupTask", ns)
        self.assertIsNotNone(startup)
        self.assertEqual(startup.get("Enabled"), "false")
        self.assertEqual(startup.get("TaskId"), "HourstoneCompanionStartup")


@unittest.skipUnless(POWERSHELL, "PowerShell 7 is required")
class MsixPackagingGates(unittest.TestCase):
    def invoke(self, *args):
        return subprocess.run([POWERSHELL, "-NoProfile", "-File", str(ROOT / "tools/package-msix.ps1"), *args],
                              cwd=ROOT, text=True, capture_output=True, timeout=15)

    def test_production_requires_real_identity(self):
        result = self.invoke("-Profile", "Store")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("requires an identity file", result.stdout + result.stderr)

    def test_local_test_cannot_use_production_identity(self):
        result = self.invoke("-Profile", "LocalTest", "-IdentityPath", "do-not-read.json")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("never uses a production identity", result.stdout + result.stderr)

    def test_production_cannot_use_test_signing(self):
        result = self.invoke("-Profile", "Store", "-SignLocalTest")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("cannot sign a Store production package", result.stdout + result.stderr)

    def test_blank_example_is_rejected(self):
        result = self.invoke("-Profile", "Store", "-IdentityPath", str(ROOT / "packaging/store-identity.example.json"))
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Missing Store identity field", result.stdout + result.stderr)

    def test_invalid_store_version_is_rejected_before_restore(self):
        temporary_root = ROOT / "artifacts" / "msix-gate-tests"
        temporary_root.mkdir(parents=True, exist_ok=True)
        for version in ("0.1.7.0", "1.0.0.1", "1.0.0", "1.65536.0.0"):
            with self.subTest(version=version), tempfile.TemporaryDirectory(dir=temporary_root) as directory:
                identity = Path(directory) / "identity.json"
                identity.write_text(json.dumps({"name": "Reserved.SyntheticProduct", "publisher": "CN=Synthetic",
                    "publisherDisplayName": "Synthetic", "storeProductId": "9SYNTHETICTEST", "packageVersion": version}), encoding="utf-8")
                result = self.invoke("-Profile", "Store", "-IdentityPath", str(identity))
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("Store package version requires", result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
