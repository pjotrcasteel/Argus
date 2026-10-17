#!/usr/bin/env python3
"""Build a review-only, iPhone-friendly site from independently captured DEVELOPMENT runs.

The produced artifact is NOT auto-published; it contains real public-game screenshots.
Only presampled images and blank human labels are copied. Trace/agent predictions stay out.
"""
import argparse
import json
import struct
from pathlib import Path

PNG_MAGIC = b"\x89PNG\r\n\x1a\n"
MAX_PNG_BYTES = 2_000_000
MAX_FRAMES = 64
TEMPLATE = Path(__file__).resolve().parents[2] / "web/mobile-annotator/index.template.html"


def prepare(source: Path, destination: Path) -> None:
    plan = json.loads((source / "study-plan.json").read_text(encoding="utf-8"))
    if plan.get("schemaVersion") != 1 or plan.get("mode") != "development-only":
        raise ValueError("Only explicitly preselected development-only runs can be packaged.")
    assignments = plan.get("assignments")
    if not isinstance(assignments, list) or not 2 <= len(assignments) <= 5:
        raise ValueError("The review package requires 2–5 independent development runs.")
    template = TEMPLATE.read_text(encoding="utf-8")
    if template.count("/*ARGUS_FRAMES*/") != 1 or template.count("/*ARGUS_LABELS*/") != 1:
        raise ValueError("Mobile template injection tokens are missing or ambiguous.")

    used = set()
    links = []
    for assignment in assignments:
        name = assignment["runId"]
        if not isinstance(name, str) or not name.startswith("dev-") or not name[4:].replace("-", "").isalnum():
            raise ValueError("Unexpected or unsafe development run identifier.")
        if name in used or assignment.get("partition") != "development":
            raise ValueError("Duplicate run or unexpected partition.")
        used.add(name)
        steps = assignment["frameSteps"]
        if not isinstance(steps, list) or not 1 <= len(steps) <= MAX_FRAMES or not all(
            isinstance(step, int) and not isinstance(step, bool) and step >= 0 for step in steps
        ) or len(set(steps)) != len(steps) or steps != sorted(steps):
            raise ValueError("Invalid preselected screenshot steps.")

        root = source / name
        labels = json.loads((root / "blind-labels.template.json").read_text(encoding="utf-8"))
        if labels.get("schemaVersion") != 1 or labels.get("runId") != name or [
            item.get("step") for item in labels.get("frames", [])
        ] != steps:
            raise ValueError("Annotation template is not tied to the frozen screenshot selection.")
        if any(item.get("player") != {"state": "unknown", "center": None} or
               item.get("threats") != {"state": "unknown", "centers": None}
               for item in labels["frames"]):
            raise ValueError("Mobile reference must remain entirely human-unknown.")

        target = destination / "annoteren" / name
        target.mkdir(parents=True, exist_ok=True)
        frames = []
        for step in steps:
            filename = f"frame-{step:04d}.png"
            img = root / filename
            if img.is_symlink():
                raise ValueError("Linked images are not accepted.")
            raw = img.read_bytes()
            if not 24 <= len(raw) <= MAX_PNG_BYTES or not raw.startswith(PNG_MAGIC):
                raise ValueError("Missing, invalid or oversized original screenshot.")
            width, height = struct.unpack(">II", raw[16:24])
            if (width, height) != (390, 844):
                raise ValueError("Screenshot dimensions do not match review viewport.")
            (target / filename).write_bytes(raw)
            frames.append({"step": step, "width": width, "height": height, "file": filename})

        def safe_json(value):
            return json.dumps(value, ensure_ascii=False, separators=(",", ":")).replace("<", "\\u003c")

        page = template.replace("/*ARGUS_FRAMES*/", safe_json(frames))
        page = page.replace("/*ARGUS_LABELS*/", safe_json(labels))
        page = page.replace("run-02 · ontwikkelset", f"{name} · ontwikkelset")
        page = page.replace("12 echte screenshots", f"{len(frames)} echte screenshots")
        page = page.replace("1 / 12", f"1 / {len(frames)}")
        page = page.replace('original.runId+"-m24"', 'original.runId+"-m26b"')
        (target / "index.html").write_text(page, encoding="utf-8")
        links.append((name, len(frames)))
    (destination / ".nojekyll").write_text("", encoding="utf-8")
    items = "".join(f'<li><a href="annoteren/{name}/">{name}: {n} screenshots beoordelen</a></li>'
                    for name, n in links)
    homepage = ('<!doctype html><html lang="nl"><meta charset="utf-8">'
                '<meta name="viewport" content="width=device-width,initial-scale=1">'
                '<title>ARGUS M2.6b blind review</title>'
                '<body style="background:#111827;color:#eff5ff;font:17px system-ui;padding:24px;max-width:700px">'
                '<h1>ARGUS M2.6b — ontwikkelbeelden</h1><p>Handmatige beoordeling op originele '
                'WyrmForge-screenshots; geen ARGUS-voorspellingen of game-internals.</p>'
                '<ul>' + items + '</ul><p>Dit is een lokaal gegenereerd reviewpakket, '
                'geen automatisch gepubliceerde website. Labels worden uitsluitend op het toestel opgeslagen '
                'tot je ze zelf exporteert.</p></body></html>')
    (destination / "index.html").write_text(homepage, encoding="utf-8")
    print(f"Prepared {len(assignments)} distinct development mobile review pages; zero human labels inferred.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--runs", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    options = parser.parse_args()
    prepare(options.runs, options.output)
