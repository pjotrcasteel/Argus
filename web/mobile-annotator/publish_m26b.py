#!/usr/bin/env python3
"""Publish only owner-approved ARGUS development screenshot review pages.

Combines the existing independent run-02 page with the frozen M2.6b dev-a/dev-b
mobile artifact. Never publishes held-out screenshots, traces, agent positions or
inferred human labels. Only whitelisted files from the archived mobile site enter Pages.
"""
import argparse
import hashlib
import json
import re
import struct
from pathlib import Path
from zipfile import ZipFile

RUNS = ("dev-a", "dev-b")
PNG_MAGIC = b"\x89PNG\r\n\x1a\n"
LIMIT = 2_000_000


def publish(archive: Path, destination: Path) -> None:
    original = destination / "annoteren" / "run-02" / "index.html"
    if not original.is_file():
        raise ValueError("Original run-02 review page must be built before adding new runs.")
    original_digest = hashlib.sha256(original.read_bytes()).hexdigest()

    manifest = {"schemaVersion": 1, "cohort": "m2-6b-owner-approved-public-development",
                "routes": ["annoteren/run-02/"], "screenshots": {}}
    with ZipFile(archive) as z:
        for run in RUNS:
            prefix = f"annoteren/{run}/"
            document = z.read(prefix + "index.html").decode("utf-8")
            if (document.count("<script>") != 1 or document.count("</script>") != 1 or
                    "/*ARGUS_FRAMES*/" in document or "/*ARGUS_LABELS*/" in document or
                    "threatLocations" in document or "playerX" in document or
                    "run-01" in document or "run-02" in document):
                raise ValueError(f"Unsafe or unblinded mobile review page: {run}")

            frame_match = re.search(r"const frames=(\[.*?\]);", document)
            label_match = re.search(r"const original=(\{.*?\});", document)
            if frame_match is None or label_match is None:
                raise ValueError("Cannot validate frozen frame/label source data.")
            frames = json.loads(frame_match.group(1))
            labels = json.loads(label_match.group(1))
            if (labels.get("schemaVersion") != 1 or labels.get("runId") != run or
                    len(frames) != 12 or len(labels.get("frames", [])) != 12):
                raise ValueError("Unexpected run identity, format or sample count.")
            if [f["step"] for f in frames] != [f["step"] for f in labels["frames"]]:
                raise ValueError("Human reference is not tied to original presampled frames.")
            if len({f["step"] for f in frames}) != 12 or [f["step"] for f in frames] != sorted(
                f["step"] for f in frames
            ):
                raise ValueError("Repeated or reordered screenshot positions.")
            if any(f.get("player") != {"state": "unknown", "center": None} or
                   f.get("threats") != {"state": "unknown", "centers": None}
                   for f in labels["frames"]):
                raise ValueError("Automatic or non-blind human reference labels are forbidden.")

            target = destination / prefix
            target.mkdir(parents=True, exist_ok=True)
            (target / "index.html").write_text(document, encoding="utf-8")
            screenshot_hashes = {}
            for frame in frames:
                step = frame["step"]
                name = f"frame-{step:04d}.png"
                if (frame.get("file") != name or frame.get("width") != 390 or
                        frame.get("height") != 844):
                    raise ValueError("Unexpected screenshot metadata or file reference.")
                data = z.read(prefix + name)
                if len(data) < 24 or len(data) > LIMIT or data[:8] != PNG_MAGIC or struct.unpack(
                    ">II", data[16:24]
                ) != (390, 844):
                    raise ValueError("Invalid original pixel screenshot.")
                (target / name).write_bytes(data)
                screenshot_hashes[name] = hashlib.sha256(data).hexdigest()
            manifest["screenshots"][run] = screenshot_hashes
            manifest["routes"].append(prefix)
            print(f"Checked {run}: twelve real 390x844 development screenshots, all labels UNKNOWN.")

    if hashlib.sha256(original.read_bytes()).hexdigest() != original_digest:
        raise ValueError("Existing run-02 reviewer was modified.")
    manifest["preservedRun02HtmlSha256"] = original_digest

    homepage = """<!doctype html><html lang="nl"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>ARGUS — Blinde ontwikkelbeelden</title>
<style>body{background:#111827;color:#eef5ff;font:17px system-ui;max-width:650px;
margin:0 auto;padding:24px;line-height:1.5}a{color:#a8e6ff}li{margin:18px 0}
small{color:#aebed4}</style></head><body>
<h1>ARGUS — WyrmForge-beelden beoordelen</h1>
<p>Beoordeel uitsluitend wat je ziet. Voorspellingen zijn verborgen; alle labels beginnen onbekend.</p>
<ul><li><a href="annoteren/dev-a/">Nieuwe development-run A — 12 screenshots</a></li>
<li><a href="annoteren/dev-b/">Nieuwe development-run B — 12 screenshots</a></li>
<li><a href="annoteren/run-02/">Oorspronkelijke run-02 — 12 screenshots</a></li></ul>
<p><small>Beoordelingen blijven op je eigen toestel tot je kiest voor kopiëren,
downloaden of delen. Download voor iedere nieuwe run afzonderlijk labels.json.</small></p>
</body></html>"""
    (destination / "index.html").write_text(homepage, encoding="utf-8")
    (destination / ".nojekyll").write_text("", encoding="utf-8")
    (destination / "published-development-manifest.json").write_text(
        json.dumps(manifest, indent=2, sort_keys=True), encoding="utf-8")
    print("Published review site includes the original page unchanged and two NEW development routes.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--archive", required=True, type=Path)
    parser.add_argument("--destination", required=True, type=Path)
    args = parser.parse_args()
    publish(args.archive, args.destination)
