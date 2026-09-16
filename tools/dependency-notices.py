"""Build a path-free dependency inventory and preserve package license notices."""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--output", required=True, type=Path)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
assets = json.loads((ROOT / "src/Hourstone.Companion.App/obj/project.assets.json").read_text(encoding="utf-8"))
package_roots = [Path(value) for value in assets["packageFolders"]]
packages = dict(assets["libraries"])
runtime_config = json.loads((args.output / "Hourstone.Companion.runtimeconfig.json").read_text(encoding="utf-8"))
for framework in runtime_config["runtimeOptions"].get("includedFrameworks", []):
    identifier = f"{framework['name']}.Runtime.win-x64/{framework['version']}"
    packages[identifier] = {"type": "package", "path": identifier.lower()}
components, notices = [], ["Hourstone Companion — third-party dependency notices", ""]
for identifier, package in sorted(packages.items()):
    if package.get("type") != "package":
        continue
    name, version = identifier.rsplit("/", 1)
    folder = next((root / package["path"] for root in package_roots if (root / package["path"]).is_dir()), None)
    if folder is None:
        raise SystemExit(f"Restore dependency before packaging: {name}")
    nuspec = next(folder.glob("*.nuspec"), None)
    if nuspec is None:
        raise SystemExit(f"Missing dependency license metadata: {name}")
    metadata = ET.parse(nuspec).getroot()
    license_element = next((e for e in metadata.iter() if e.tag.rsplit("}", 1)[-1] == "license"), None)
    license_text = license_element.text if license_element is not None else None
    license_type = license_element.get("type") if license_element is not None else None
    component = {"type": "library", "name": name, "version": version, "purl": f"pkg:nuget/{name}@{version}"}
    if license_text and license_type == "expression":
        component["licenses"] = [{"expression": license_text}]
    else:
        component["licenses"] = [{"license": {"name": license_text or "See package license notices"}}]
    component["externalReferences"] = [{"type": "distribution", "url": f"https://www.nuget.org/packages/{name}/{version}"}]
    components.append(component)
    notices.extend([f"{name} {version}", f"Package: https://www.nuget.org/packages/{name}/{version}",
                    f"License: {license_text or 'See package metadata'}", ""])
    license_files = set()
    if license_type == "file" and license_text:
        candidate = (folder / license_text).resolve()
        if candidate.is_relative_to(folder.resolve()) and candidate.is_file():
            license_files.add(candidate)
    for candidate in folder.rglob("*"):
        if candidate.is_file() and candidate.stat().st_size < 1024 * 1024:
            stem = candidate.name.upper()
            if stem.startswith(("LICENSE", "LICENCE", "NOTICE", "COPYING", "THIRD-PARTY-NOTICES")) and candidate.suffix.lower() in ("", ".txt", ".md"):
                license_files.add(candidate.resolve())
    vendor_name = ("Velopack-1.2.0.txt" if name == "Velopack" and version == "1.2.0" else
                   "SQLitePCLRaw-2.1.12.txt" if name.startswith("SQLitePCLRaw.") and version == "2.1.12" else
                   "Microsoft-10.0.12.txt" if name.startswith("Microsoft.") and version == "10.0.12" else None)
    if vendor_name:
        license_files.add(ROOT / "docs/licenses" / vendor_name)
    if not license_files:
        raise SystemExit(f"Missing bundled license text for {name} {version}; review its license when upgrading.")
    seen = set()
    for candidate in sorted(license_files):
        content = candidate.read_text(encoding="utf-8-sig", errors="replace").strip()
        digest = hashlib.sha256(content.encode()).hexdigest()
        if digest not in seen:
            notices.extend([content, ""])
            seen.add(digest)
    notices.append("-" * 72)
version = ET.parse(ROOT / "Directory.Build.props").getroot().find(".//Version").text
sbom = {"bomFormat": "CycloneDX", "specVersion": "1.6", "version": 1,
        "metadata": {"component": {"type": "application", "name": "Hourstone Companion", "version": version}},
        "components": components}
(args.output / "dependencies.cdx.json").write_text(json.dumps(sbom, indent=2) + "\n", encoding="utf-8")
(args.output / "DEPENDENCY-NOTICES.txt").write_text("\n".join(notices) + "\n", encoding="utf-8")
print(f"PASS dependency inventory: {len(components)} NuGet packages")
