#!/usr/bin/env python3
"""21 synthetic screenshot frames: contrast easy background with textured absent-player traps.

This is artificial reference truth only, NEVER private human annotations or true game success.
"""
import json
import struct
import zlib
from pathlib import Path

WIDTH, HEIGHT = 390, 844
root = Path("artifacts/ci-hard-negatives/run-local")
root.mkdir(parents=True, exist_ok=True)


def chunk(tag, payload):
    return struct.pack(">I", len(payload)) + tag + payload + struct.pack(
        ">I", zlib.crc32(tag + payload) & 0xffffffff)


def paint(rgb, x, y, width, height, color):
    for row in range(y, y + height):
        for col in range(x, x + width):
            at = (row * WIDTH + col) * 3
            rgb[at:at + 3] = bytes(color)


def make_png(scene):
    rgb = bytearray(bytes((30, 39, 34)) * WIDTH * HEIGHT)
    if scene == "player":
        paint(rgb, 184, 405, 20, 30, (72, 91, 115))
    elif scene == "occluded_wide":
        paint(rgb, 165, 400, 60, 36, (72, 91, 115))
    elif scene == "occluded_small":
        paint(rgb, 190, 416, 9, 7, (72, 91, 115))
    elif scene == "textured_absent":
        # Strong red terrain contrast, but NO detectable blue actor palette.
        for y in range(395, 447):
            for x in range(162, 229):
                if (x + y) % 2 == 0:
                    at = (y * WIDTH + x) * 3
                    rgb[at:at + 3] = bytes((115, 48, 43))
    elif scene == "blue_absent":
        # Player-color imposter deliberately TOO WIDE and TOO TALL for the original
        # component acceptance rule, but still blue and richly textured in the patch.
        paint(rgb, 160, 390, 70, 75, (72, 91, 115))
    elif scene != "empty":
        raise ValueError("Unknown synthetic scene")

    pixels = b"".join(b"\x00" + rgb[y * WIDTH * 3:(y + 1) * WIDTH * 3] for y in range(HEIGHT))
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(
        ">IIBBBBB", WIDTH, HEIGHT, 8, 2, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(pixels, 6)) + chunk(b"IEND", b""))


# Each hypothesis needs two visually observed player frames; a third gap expires it.
scenes = [
    ("menu", "empty", "absent"),
    ("exploring", "player", "visible"),  # 1
    ("exploring", "player", "visible"),  # 2
    ("exploring", "occluded_wide", "visible"),  # 3: unresolved real occlusion
    ("exploring", "occluded_small", "visible"),  # 4: unresolved real occlusion
    ("exploring", "player", "visible"),  # 5
    ("exploring", "player", "visible"),  # 6
    ("exploring", "empty", "absent"),  # 7: easy veto
    ("exploring", "empty", "absent"),  # 8: easy veto
    ("exploring", "empty", "absent"),  # 9: expires
    ("exploring", "player", "visible"),  # 10
    ("exploring", "player", "visible"),  # 11
    ("exploring", "textured_absent", "absent"),  # 12: hard negative (terrain)
    ("exploring", "textured_absent", "absent"),  # 13: hard negative (terrain)
    ("exploring", "empty", "absent"),  # 14: expires
    ("exploring", "player", "visible"),  # 15
    ("exploring", "player", "visible"),  # 16
    ("exploring", "blue_absent", "absent"),  # 17: hard negative (blue decoy)
    ("exploring", "blue_absent", "absent"),  # 18: hard negative (blue decoy)
    ("exploring", "empty", "absent"),  # 19: expires
    ("menu", "empty", "absent")
]
trace, labels = [], []
for step, (phase, scene, human) in enumerate(scenes):
    name = f"frame-{step:04d}.png"
    (root / name).write_bytes(make_png(scene))
    evidence = {"phase": phase, "reason": "artificial M2.12 hard-negative cohort"}
    if scene == "player":
        evidence["combat"] = {"playerX": 193.5, "playerY": 419.5,
                              "threatCandidates": 0, "threatLocations": []}
    trace.append({"step": step, "timestampUtc": "2026-10-17T10:00:00Z",
                  "width": WIDTH, "height": HEIGHT, "screenshot": name, "evidence": evidence})
    labels.append({"step": step, "player": {"state": human, "center": {
        "x": 193, "y": 420} if human == "visible" else None},
                   "threats": {"state": "known", "centers": []}})

(root / "trace.jsonl").write_text("\n".join(json.dumps(frame) for frame in trace) + "\n")
(root / "labels.json").write_text(json.dumps({
    "schemaVersion": 1, "runId": "run-local", "frames": labels
}))
print("Synthetic only: 2 visible occlusions, 2 blank absences, 2 red decor, 2 blue decoys.")
