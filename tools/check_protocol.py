"""Verify the exact vendored synchronization contract without network access."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
pin = json.loads((ROOT / "docs/protocol-pin.json").read_text(encoding="utf-8"))
for relative, expected in pin["files"].items():
    target = (ROOT / relative).resolve()
    if not target.is_relative_to(ROOT):
        raise SystemExit("Invalid protocol file path")
    if hashlib.sha256(target.read_bytes()).hexdigest() != expected:
        raise SystemExit(f"Protocol content differs from its pin: {relative}")
print(f"PASS protocol {pin['formatVersion']}: {len(pin['files'])} pinned files")
