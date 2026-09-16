"""Verify bundled icon integrity offline using only the Python standard library."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import struct
import zlib

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "src/Hourstone.Companion.App/Assets"
CLASSES = {
    "WARRIOR", "PALADIN", "HUNTER", "ROGUE", "PRIEST", "DEATHKNIGHT",
    "SHAMAN", "MAGE", "WARLOCK", "MONK", "DRUID", "DEMONHUNTER", "EVOKER",
}
CLIENTS = {"retail", "mists", "tbc", "era"}


def png_dimensions(data: bytes) -> tuple[int, int]:
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("invalid PNG signature")
    offset = 8
    dimensions = None
    image_data = bytearray()
    chunks = []
    while offset < len(data):
        if len(data) - offset < 12:
            raise ValueError("truncated PNG chunk")
        size = struct.unpack_from(">I", data, offset)[0]
        kind = data[offset + 4:offset + 8]
        end = offset + 12 + size
        if end > len(data):
            raise ValueError("PNG chunk exceeds file size")
        payload = data[offset + 8:offset + 8 + size]
        checksum = struct.unpack_from(">I", data, offset + 8 + size)[0]
        if zlib.crc32(kind + payload) & 0xFFFFFFFF != checksum:
            raise ValueError("PNG chunk checksum mismatch")
        if kind == b"IHDR":
            if chunks or len(payload) != 13:
                raise ValueError("invalid PNG header")
            dimensions = struct.unpack_from(">II", payload)
        if kind == b"IDAT":
            image_data.extend(payload)
        chunks.append(kind)
        offset = end
        if kind == b"IEND":
            if size or offset != len(data):
                raise ValueError("invalid PNG ending")
            break
    if not dimensions or not image_data or chunks[-1] != b"IEND":
        raise ValueError("incomplete PNG")
    if not zlib.decompress(image_data):
        raise ValueError("empty PNG image stream")
    return dimensions


def verify() -> int:
    manifest = json.loads((ROOT / "docs/assets-manifest.json").read_text(encoding="utf-8"))
    if manifest.get("schema_version") != 1:
        raise ValueError("unsupported asset manifest")
    expected = {"class:" + token for token in CLASSES}
    expected |= {"client:" + flavor for flavor in CLIENTS}
    expected |= {"heart", "unknown"}
    entries = manifest["assets"]
    if len(entries) != len(expected) or {entry["id"] for entry in entries} != expected:
        raise ValueError("catalog must contain exactly 13 classes, four clients, heart, and unknown")
    catalog_files = set()
    for entry in entries:
        target = (ROOT / entry["file"]).resolve()
        if not target.is_relative_to(ASSETS.resolve()):
            raise ValueError("asset is outside the resource directory")
        if target in catalog_files:
            raise ValueError("duplicate asset file")
        catalog_files.add(target)
        data = target.read_bytes()
        if hashlib.sha256(data).hexdigest() != entry["sha256"]:
            raise ValueError(f"asset checksum mismatch: {entry['id']}")
        size = png_dimensions(data)
        if size != (entry["width"], entry["height"]):
            raise ValueError(f"asset dimensions mismatch: {entry['id']}")
        if entry["id"].startswith("class:") and size != (56, 56):
            raise ValueError("class icons must retain native dimensions")
        if entry["id"] in {"heart", "client:era"} and size != (32, 32):
            raise ValueError("heart and Era icon must retain native dimensions")
    actual_files = set((ASSETS / "Classes").glob("*.png"))
    actual_files |= set((ASSETS / "Clients").glob("*.png"))
    actual_files |= {ASSETS / "Heart.png", ASSETS / "Unknown.png"}
    if {path.resolve() for path in actual_files} != catalog_files:
        raise ValueError("catalog images and manifest differ")
    if {path.stem for path in (ASSETS / "Classes").glob("*.png")} != CLASSES:
        raise ValueError("class resource names do not match the WoW tokens")
    if {path.stem for path in (ASSETS / "Clients").glob("*.png")} != CLIENTS:
        raise ValueError("client resource names do not match protocol families")
    return len(entries)


if __name__ == "__main__":
    print(f"Verified {verify()} bundled icons offline (13 classes, 4 clients, heart, unknown).")
