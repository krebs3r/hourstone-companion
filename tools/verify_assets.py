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


def verify_app_icon() -> None:
    manifest = json.loads((ROOT / "docs/app-icon.json").read_text(encoding="utf-8"))
    icon = ASSETS / "Hourstone.ico"
    logo = ASSETS / "Logo.png"
    if manifest["file"] != icon.relative_to(ROOT).as_posix() or manifest["source"] != logo.relative_to(ROOT).as_posix():
        raise ValueError("unexpected application icon source")
    data = icon.read_bytes()
    if hashlib.sha256(data).hexdigest() != manifest["sha256"] or hashlib.sha256(logo.read_bytes()).hexdigest() != manifest["source_sha256"]:
        raise ValueError("application icon or source checksum mismatch")
    expected = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    if manifest["sizes"] != expected or len(data) < 6 + 16 * len(expected):
        raise ValueError("application icon resolutions missing")
    if struct.unpack_from("<HHH", data) != (0, 1, len(expected)):
        raise ValueError("invalid application ICO header")
    next_offset = 6 + 16 * len(expected)
    for index, size in enumerate(expected):
        width, height, colors, reserved, planes, depth, length, offset = struct.unpack_from("<BBBBHHII", data, 6 + index * 16)
        if (width or 256, height or 256, colors, reserved, planes, depth) != (size, size, 0, 0, 1, 32):
            raise ValueError("invalid ICO frame metadata")
        if offset != next_offset or length < 33 or offset + length > len(data):
            raise ValueError("invalid ICO frame bounds")
        if png_dimensions(data[offset:offset + length]) != (size, size):
            raise ValueError("ICO frame dimensions mismatch")
        next_offset += length
    if next_offset != len(data):
        raise ValueError("unexpected application icon payload")


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
    verify_app_icon()
    return len(entries)


if __name__ == "__main__":
    print(f"Verified {verify()} bundled icons offline (13 classes, 4 clients, heart, unknown), plus the nine-resolution Windows application icon.")
