#!/usr/bin/env python3
"""Synthetic black-box run fixtures for M2.14 frozen 24-frame blind review, NEVER game truth."""
import hashlib
import json
import struct
import zlib
from pathlib import Path

ROOT = Path("artifacts/ci-m214")
WIDTH, HEIGHT = 390, 844
SEED = "argus-m214-blind-24-bins-development-only-20261018"
RUNS = ("dev-f", "dev-g", "dev-h")


def png_chunk(kind, data):
    return (struct.pack(">I", len(data)) + kind + data
            + struct.pack(">I", zlib.crc32(kind + data) & 0xffffffff))


raw = b"".join(b"\0" + bytes([30, 39, 34]) * WIDTH for _ in range(HEIGHT))
png = (b"\x89PNG\r\n\x1a\n" + png_chunk(b"IHDR", struct.pack(
    ">IIBBBBB", WIDTH, HEIGHT, 8, 2, 0, 0, 0))
    + png_chunk(b"IDAT", zlib.compress(raw, 8)) + png_chunk(b"IEND", b""))

for run in RUNS:
    folder = ROOT / run
    folder.mkdir(parents=True, exist_ok=True)
    trace = []
    for step in range(55):
        filename = f"frame-{step:04d}.png"
        (folder / filename).write_bytes(png)
        trace.append({
            "step": step, "timestampUtc": "2026-10-18T06:00:00Z",
            "width": WIDTH, "height": HEIGHT, "screenshot": filename,
            "evidence": {
                "phase": "exploring" if 8 < step < 52 else "menu",
                "reason": f"synthetic-only {run} frame {step}"
            }
        })
    encoded = "\n".join(json.dumps(row) for row in trace) + "\n"
    (folder / "trace.jsonl").write_text(encoded)
    print(run, "frames", len(trace), "trace_sha256", hashlib.sha256(encoded.encode()).hexdigest())

assert len(RUNS) == 3
print("Fake 55-frame run captures created. All human labels remain unknown.")
