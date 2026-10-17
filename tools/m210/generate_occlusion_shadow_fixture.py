#!/usr/bin/env python3
"""Create transparent synthetic image-only evidence with both true and false bridge hypotheses."""
import json
import struct
import zlib
from pathlib import Path

WIDTH, HEIGHT = 390, 844
root = Path("artifacts/ci-occlusion-shadow/run-local")
root.mkdir(parents=True, exist_ok=True)


def chunk(tag, payload):
    return struct.pack(">I", len(payload)) + tag + payload + struct.pack(
        ">I", zlib.crc32(tag + payload) & 0xffffffff)


def png(rect):
    image = bytearray(bytes((30, 39, 34)) * (WIDTH * HEIGHT))
    if rect is not None:
        left, top, width, height = rect
        for y in range(top, top + height):
            for x in range(left, left + width):
                offset = (y * WIDTH + x) * 3
                image[offset:offset + 3] = bytes((72, 91, 115))
    scanlines = b"".join(b"\x00" + image[y * WIDTH * 3:(y + 1) * WIDTH * 3]
                         for y in range(HEIGHT))
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(
        ">IIBBBBB", WIDTH, HEIGHT, 8, 2, 0, 0, 0))
    + chunk(b"IDAT", zlib.compress(scanlines, 6)) + chunk(b"IEND", b"")


# Two actual observations, two artificial occlusions, two observations,
# two frames where the person is human-confirmed ABSENT, a third expiry, menu.
scenes = [
    ("menu", (184, 405, 20, 30), False, "visible"),
    ("exploring", (184, 405, 20, 30), True, "visible"),
    ("exploring", (184, 405, 20, 30), True, "visible"),
    ("exploring", (165, 400, 60, 36), False, "visible"),
    ("exploring", (190, 416, 9, 7), False, "visible"),
    ("exploring", (184, 405, 20, 30), True, "visible"),
    ("exploring", (184, 405, 20, 30), True, "visible"),
    ("exploring", None, False, "absent"),
    ("exploring", None, False, "absent"),
    ("exploring", None, False, "absent"),
    ("menu", None, False, "absent"),
]
rows, labels = [], []
for step, (phase, rect, detected, truth) in enumerate(scenes):
    name = f"frame-{step:04d}.png"
    (root / name).write_bytes(png(rect))
    evidence = {"phase": phase, "reason": "artificial bounded player loss experiment"}
    if detected:
        evidence["combat"] = {"playerX": 193.5, "playerY": 419.5,
                              "threatCandidates": 0, "threatLocations": []}
    rows.append({"step": step, "timestampUtc": "2026-10-17T10:00:00Z",
                 "width": WIDTH, "height": HEIGHT, "screenshot": name, "evidence": evidence})
    labels.append({"step": step,
                   "player": {"state": truth, "center": {"x": 193, "y": 420} if truth == "visible" else None},
                   "threats": {"state": "known", "centers": []}})

(root / "trace.jsonl").write_text("\n".join(json.dumps(row) for row in rows) + "\n")
(root / "labels.json").write_text(json.dumps({"schemaVersion": 1, "runId": "run-local", "frames": labels}))
print("Created 11 image-only synthetic frames, including two HUMAN-ABSENT false bridge traps.")
