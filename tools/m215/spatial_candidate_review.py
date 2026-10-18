#!/usr/bin/env python3
"""Predeclared candidate-LOCATION reference protocol. All reference states remain UNKNOWN.

Input must be a previously recorded development-only M2.14 original-evidence ZIP.
Nothing uses agent detections, player predictions, outcomes or human labels for sampling.
The output remains LOCAL, not auto-published to GitHub Pages.
"""
import argparse
import hashlib
import json
import re
import struct
import zipfile
from pathlib import Path

SEED = "argus-m215-independent-spatial-probes-20261018-v1"
SOURCE_SEED = "argus-m214-blind-24-bins-development-only-20261018"
RUNS = ("dev-f", "dev-g", "dev-h")
PNG_HEADER = b"\x89PNG\r\n\x1a\n"
WIDTH, HEIGHT = 390, 844
RADIUS = 26
PROBES_PER_RUN = 24
FRAME_COUNT = 12
MAX_PNG_BYTES = 2_000_000
CENTER = (195, 422)
GRID = ((72, 180), (195, 180), (318, 180),
        (72, 422), (318, 422),
        (72, 650), (195, 650), (318, 650))
ALLOWED_LABELS = {"present", "absent", "unknown"}
TEMPLATE = Path(__file__).resolve().parents[2] / "web/candidate-annotator/index.template.html"


def pick(seed: str, run: str, salt: str, n: int) -> int:
    if n < 1:
        raise ValueError("Cannot select from an empty eligible spatial bin.")
    return int.from_bytes(hashlib.sha256(f"{seed}\n{run}\n{salt}".encode()).digest()[:8], "big") % n


def read_json(archive: zipfile.ZipFile, name: str):
    return json.loads(archive.read(name))


def build(archive_path: Path, destination: Path) -> dict:
    if destination.exists() and any(destination.iterdir()):
        raise ValueError("Refuse to overwrite an existing review site or human annotations.")
    with zipfile.ZipFile(archive_path) as z:
        plan = read_json(z, "study-plan.json")
        if (plan.get("schemaVersion") != 1 or plan.get("mode") != "development-only"
                or plan.get("seed") != SOURCE_SEED or plan.get("requestedFramesPerRun") != 24):
            raise ValueError("Source is not the frozen M2.14 development-only reference plan.")
        assignments = plan.get("assignments")
        if not isinstance(assignments, list) or sorted(x.get("runId") for x in assignments) != list(RUNS):
            raise ValueError("Unexpected development identities: do not access held-out datasets.")

        trace_hashes = set()
        all_runs = []
        for assignment in sorted(assignments, key=lambda a: a["runId"]):
            run = assignment["runId"]
            if assignment.get("partition") != "development":
                raise ValueError("Held-out source partition rejected.")
            steps = assignment.get("frameSteps")
            if (not isinstance(steps, list) or len(steps) != 24 or steps != sorted(set(steps))
                    or any(type(step) is not int or step < 0 for step in steps)):
                raise ValueError("Frozen original M2.14 temporal sample changed.")
            prefix = run + "/"
            trace_bytes = z.read(prefix + "trace.jsonl")
            sha = hashlib.sha256(trace_bytes).hexdigest()
            if sha in trace_hashes:
                raise ValueError("Repeated trace hash is not an independent run.")
            trace_hashes.add(sha)
            originals = [json.loads(row) for row in trace_bytes.decode().splitlines()]
            if any(row.get("step") != idx for idx, row in enumerate(originals)):
                raise ValueError("Source trace lacks strictly sequential image frame steps.")
            if not steps or steps[-1] >= len(originals):
                raise ValueError("Sample indices out of source screenshot bounds.")
            # Phase is an observable UI-state classification from the frozen ORIGINAL run,
            # not a prediction of where any sprite should be. Predetermine this rule for all runs.
            eligible = [step for step in steps if originals[step]["evidence"]["phase"] == "exploring"]
            if len(eligible) < FRAME_COUNT:
                raise ValueError(f"{run} insufficient preselected exploring frames; no retroactive cherry picking.")

            # Exactly one fixed-seed draw in each of twelve chronological bins of the
            # eligible part of the already-immutable 24-frame M2.14 sample.
            chosen = []
            for bin_id in range(FRAME_COUNT):
                start = bin_id * len(eligible) // FRAME_COUNT
                end = (bin_id + 1) * len(eligible) // FRAME_COUNT
                chosen.append(eligible[start + pick(SEED, run, f"temporal-{bin_id}", end - start)])
            if len(set(chosen)) != FRAME_COUNT or chosen != sorted(chosen):
                raise AssertionError("Temporal bins must select distinct chronological frames.")

            images = []
            probes = []
            target = destination / "annoteren" / run
            target.mkdir(parents=True, exist_ok=True)
            for ordinal, step in enumerate(chosen):
                name = f"frame-{step:04d}.png"
                if originals[step].get("screenshot") != name:
                    raise ValueError("Original PNG path doesn't match frozen trace.")
                png = z.read(prefix + name)
                if (len(png) > MAX_PNG_BYTES or len(png) < 24 or not png.startswith(PNG_HEADER)
                        or struct.unpack(">II", png[16:24]) != (WIDTH, HEIGHT)):
                    raise ValueError("Original 390x844 image missing/unsafe.")
                (target / name).write_bytes(png)
                images.append({"step": step, "file": name, "sha256": hashlib.sha256(png).hexdigest()})
                other = GRID[pick(SEED, run, f"spatial-{step}", len(GRID))]
                for role, (x, y) in (("center-anchor", CENTER), ("spatial-control", other)):
                    probes.append({"sampleId": f"{run}-step{step:04d}-{role}",
                                   "step": step, "x": x, "y": y, "radius": RADIUS,
                                   "role": role, "label": "unknown"})

            overview = {"schemaVersion": 1, "runId": run, "partition": "development",
                        "traceSha256": sha, "images": images,
                        "candidateCount": len(probes), "candidates": probes}
            # No player center, threat inference, or game-state prediction is embedded.
            template = TEMPLATE.read_text(encoding="utf-8")
            if template.count("/*ARGUS_STUDY*/") != 1:
                raise ValueError("Only one safe offline reviewer data injection is allowed.")
            payload = json.dumps({"seed": SEED, **overview}, ensure_ascii=False,
                                 separators=(",", ":")).replace("<", "\\u003c")
            (target / "index.html").write_text(template.replace("/*ARGUS_STUDY*/", payload),
                                               encoding="utf-8")
            all_runs.append(overview)

    manifest = {"schemaVersion": 1, "mode": "development-only",
                "sourceSeed": SOURCE_SEED, "studySeed": SEED, "radiusPixels": RADIUS,
                "sampling": "12 deterministic temporal-bin exploring screenshots; center + seeded grid control per image",
                "humanTruth": "ALL_UNKNOWN", "runIds": list(RUNS), "runs": all_runs}
    destination.mkdir(parents=True, exist_ok=True)
    (destination / "spatial-study-plan.json").write_text(json.dumps(manifest, indent=2) + "\n",
                                                         encoding="utf-8")
    (destination / ".nojekyll").write_text("", encoding="utf-8")
    anchors = "".join(f'<li><a href="annoteren/{r}/">{r}: 24 locaties op 12 beelden</a></li>'
                      for r in RUNS)
    (destination / "index.html").write_text(
        '<!doctype html><html lang="nl"><meta charset="utf-8">'
        '<meta name="viewport" content="width=device-width,initial-scale=1">'
        '<body style="font:18px system-ui;background:#111827;color:#eff5ff;padding:22px">'
        '<h1>ARGUS M2.15 — privé kandidaatlocaties beoordelen</h1>'
        '<p>Geen voorspellingen, geen vooraf ingevulde menselijke labels.'
        ' Alleen lokaal bewaren en JSON exporteren. Nog niet gepubliceerd.</p>'
        '<ul>' + anchors + "</ul></body></html>", encoding="utf-8")
    print("M2.15 PREDECLARED: 3 independent runs, 36 source PNGs, 72 UNKNOWN spatial probes.")
    return manifest


def validate_export(manifest_path: Path, export_path: Path) -> dict:
    """Reject any fabricated/missing/out-of-plan candidate, without calling unknown absent."""
    plan = json.loads(manifest_path.read_text(encoding="utf-8"))
    result = json.loads(export_path.read_text(encoding="utf-8"))
    run = result.get("runId")
    matches = [entry for entry in plan["runs"] if entry["runId"] == run]
    if (len(matches) != 1 or result.get("schemaVersion") != 1
            or result.get("studySeed") != plan["studySeed"]):
        raise ValueError("Reviewed export doesn't match preregistered development-only source.")
    expected = matches[0]["candidates"]
    found = result.get("probes")
    if not isinstance(found, list) or len(found) != len(expected):
        raise ValueError("Missing or additional probes in reviewer export.")
    seen = set()
    counts = {"present": 0, "absent": 0, "unknown": 0}
    expected_by_id = {item["sampleId"]: item for item in expected}
    for item in found:
        if not isinstance(item, dict) or set(item) != {"sampleId", "step", "x", "y", "radius", "role", "label"}:
            raise ValueError("Unrecognized review fields.")
        key = item["sampleId"]
        if key not in expected_by_id or key in seen:
            raise ValueError("Duplicate/unregistered candidate reference.")
        seen.add(key)
        matched = expected_by_id[key]
        if any(item[k] != matched[k] or type(item[k]) is not type(matched[k])
               for k in ("step", "x", "y", "radius", "role")):
            raise ValueError("Reviewer cannot move or replace the frozen candidate circle.")
        label = item["label"]
        if label not in ALLOWED_LABELS:
            raise ValueError("Only human-chosen present/absent/unknown allowed.")
        counts[label] += 1
    return {"runId": run, "originalCandidates": len(expected), "humanReviewed": counts["present"] + counts["absent"],
            "labels": counts, "status": "NOT_EVALUATED" if counts["present"] + counts["absent"] == 0 else "REVIEWED_ONLY",
            "trainingReady": False}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="command", required=True)
    make = sub.add_parser("build")
    make.add_argument("--archive", type=Path, required=True)
    make.add_argument("--output", type=Path, required=True)
    check = sub.add_parser("validate")
    check.add_argument("--manifest", type=Path, required=True)
    check.add_argument("--export", type=Path, required=True)
    args = parser.parse_args()
    if args.command == "build":
        build(args.archive, args.output)
    else:
        print(json.dumps(validate_export(args.manifest, args.export), indent=2))
