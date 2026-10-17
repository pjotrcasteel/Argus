#!/usr/bin/env python3
"""Only synthetic RGB PNGs and reference coordinates; no real owner-labelled data in CI."""
import json
import struct
import zlib
from pathlib import Path

width, height = 390, 844
folder = Path("artifacts/ci-causal-appearance/run-local")
folder.mkdir(parents=True, exist_ok=True)


def chunk(tag, contents):
    return struct.pack(">I", len(contents)) + tag + contents + struct.pack(
        ">I", zlib.crc32(tag + contents) & 0xffffffff)


def paint(rgb, x, y, w, h, color):
    for row in range(y, y + h):
        for col in range(x, x + w):
            index = (row * width + col) * 3
            rgb[index:index + 3] = bytes(color)


rows = []
for step, player_x in enumerate((180, 235, 290, 335, 370)):
    rgb = bytearray(bytes([30, 39, 34]) * width * height)
    paint(rgb, player_x, 405, 20, 30, (72, 91, 115))
    paint(rgb, 100, 320, 20, 28, (120, 84, 66))
    pixels = b"".join(b"\x00" + rgb[y * width * 3:(y + 1) * width * 3] for y in range(height))
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(
        ">IIBBBBB", width, height, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(pixels, 9)) + chunk(b"IEND", b"")
    name = f"frame-{step:04}.png"
    (folder / name).write_bytes(png)
    rows.append({"step": step, "timestampUtc": "2026-10-17T10:00:00Z",
                 "width": width, "height": height, "screenshot": name,
                 "evidence": {"phase": "exploring", "reason": "RGB fixture only"}})

(folder / "trace.jsonl").write_text("\n".join(json.dumps(row) for row in rows) + "\n")
point = lambda x, y: {"x": x, "y": y}
(folder / "labels.json").write_text(json.dumps({
    "schemaVersion": 1, "runId": "run-local",
    "frames": [{"step": 4, "player": {"state": "visible", "center": point(379, 420)},
                "threats": {"state": "known", "centers": [point(109, 333)]}}]
}))
print("Generated 5 consecutive synthetic game PNGs with one label only on an edge-player final frame.")
