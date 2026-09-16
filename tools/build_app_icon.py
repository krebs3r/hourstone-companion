"""Rebuild the Windows ICO from the unchanged Hourstone logo (Pillow 12.3.0)."""
from __future__ import annotations

import hashlib
import io
import json
import math
from pathlib import Path
import struct

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)


def build() -> None:
    source = ROOT / "src/Hourstone.Companion.App/Assets/Logo.png"
    target = source.with_name("Hourstone.ico")
    logo = Image.open(source).convert("RGBA")
    # Ignore near-transparent export dust when locating the visible illustration.
    # The original PNG and every visible part of the motif remain unchanged.
    bounds = logo.getchannel("A").point(lambda alpha: 255 if alpha >= 4 else 0).getbbox()
    if bounds is None:
        raise ValueError("Logo has no visible pixels")
    left, top, right, bottom = bounds
    side = max(math.ceil(max(logo.size) / 1.15), right - left + 32, bottom - top + 32)
    x, y = (left + right - side) // 2, (top + bottom - side) // 2
    crop = (x, y, x + side, y + side)
    canvas = logo.crop(crop)
    frames = []
    for size in SIZES:
        stream = io.BytesIO()
        canvas.resize((size, size), Image.Resampling.LANCZOS).save(stream, format="PNG")
        frames.append(stream.getvalue())
    offset = 6 + 16 * len(SIZES)
    entries = []
    for size, frame in zip(SIZES, frames):
        entries.append(struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(frame), offset))
        offset += len(frame)
    data = struct.pack("<HHH", 0, 1, len(SIZES)) + b"".join(entries) + b"".join(frames)
    target.write_bytes(data)
    manifest = {
        "source": source.relative_to(ROOT).as_posix(),
        "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
        "file": target.relative_to(ROOT).as_posix(),
        "sha256": hashlib.sha256(data).hexdigest(),
        "sizes": list(SIZES),
        "source_crop": list(crop),
        "transformation": "Approximately 15% larger visible motif; original logo, colors and aspect ratio retained. Independently resampled PNG frames in ICO.",
    }
    (ROOT / "docs/app-icon.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print("Rebuilt Windows application icon in nine resolutions.")


if __name__ == "__main__":
    build()
