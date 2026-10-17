#!/usr/bin/env python3
"""Build artificial original-pixel screenshots: never use private human review in public CI."""
import json
import struct
import zlib
from pathlib import Path

W, H = 390, 844
root = Path("artifacts/ci-player-loss/run-local")
root.mkdir(parents=True, exist_ok=True)


def chunk(tag, value):
    return struct.pack(">I", len(value)) + tag + value + struct.pack(
        ">I", zlib.crc32(tag + value) & 0xffffffff)


def pixel_png(rect):
    background = bytes([30, 39, 34])
    raw = bytearray(background * (W * H))
    if rect:
        x, y, width, height = rect
        for yy in range(y, y + height):
            for xx in range(x, x + width):
                index = (yy * W + xx) * 3
                raw[index:index + 3] = bytes([72, 91, 115])
    scan = b"".join(b"\x00" + raw[y * W * 3:(y + 1) * W * 3] for y in range(H))
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(
        ">IIBBBBB", W, H, 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(scan, 6)) + chunk(b"IEND", b"")


scenes = [
    ("menu", (184, 405, 20, 30), False),
    ("exploring", (184, 405, 20, 30), True),
    ("exploring", (165, 400, 60, 36), False),
    ("exploring", (190, 416, 9, 7), False),
    ("exploring", (184, 405, 20, 30), True),
]
trace, labels = [], []
for step, (phase, actor, recorded) in enumerate(scenes):
    name = f"frame-{step:04d}.png"
    (root / name).write_bytes(pixel_png(actor))
    evidence = {"phase": phase, "reason": "synthetic color audit"}
    if recorded:
        evidence["combat"] = {"playerX": 193.5, "playerY": 419.5,
                              "threatCandidates": 0, "threatLocations": []}
    trace.append({"step": step, "timestampUtc": "2026-10-17T10:00:00Z",
                  "width": W, "height": H, "screenshot": name, "evidence": evidence})
    labels.append({"step": step, "player": {"state": "visible", "center": {
        "x": 194 if step in (2, 3) else 193, "y": 420}},
                   "threats": {"state": "known", "centers": []}})
(root / "trace.jsonl").write_text("\n".join(json.dumps(item) for item in trace) + "\n")
(root / "labels.json").write_text(json.dumps({"schemaVersion": 1, "runId": "run-local", "frames": labels}))
print("5 synthetic PNGs, 2 color failures, and 1 menu transition with a visible reference.")
