#!/usr/bin/env python3
"""M2.20: freeze a NEW independent, image-only, development cohort.

Never select based on ARGUS predicted player, game outcome, center, human labels
or model score. Exactly one deterministic original screenshot from each of 18
chronological bins across each independently captured game session.

Reviewer package is NOT automatically published; all human labels stay on-device.
"""
import argparse
import hashlib
import json
import re
import shutil
import struct
from pathlib import Path

RUNS = ("dev-i", "dev-j", "dev-k")
SEED = "argus-m220-independent-blind-player-point-20261018-v1"
SAMPLES = 18
WIDTH, HEIGHT = 390, 844
PNG = b"\x89PNG\r\n\x1a\n"
TEMPLATE = Path(__file__).resolve().parents[2] / "web/point-annotator/index.template.html"


def sha(b: bytes) -> str:
    return hashlib.sha256(b).hexdigest()


def require(ok: bool, why: str) -> None:
    if not ok:
        raise ValueError(why)


def frozen_choice(run: str, bin_index: int, length: int) -> int:
    require(length > 0, "Empty chronological bin.")
    digest = hashlib.sha256(f"{SEED}\n{run}\nchronological-{bin_index}".encode()).digest()
    return int.from_bytes(digest[:8], "big") % length


def reviewer_html(study: dict) -> str:
    """Strictly reuse existing mobile point UX, but new cohort IDs/count/storage."""
    code = TEMPLATE.read_text(encoding="utf-8")
    replacements = [
        ('/^dev-[fgh]$/', '/^dev-[ijk]$/'),
        ('study.images.length!==12', 'study.images.length!==18'),
        ('a.length===12', 'a.length===images.length'),
        ('idx===11', 'idx===images.length-1'),
        ('if(idx<11)', 'if(idx<images.length-1)'),
        ('" / 12 · "+', '" / "+images.length+" · "+'),
        ('"argus-m218-player-point-"', '"argus-m220-player-point-"'),
        ('"m218-"', '"m220-"'),
        ('ARGUS M2.18 · ', 'ARGUS M2.20 · ')
    ]
    for old, new in replacements:
        require(code.count(old) == 1, f"Original blind mobile UI changed: {old}.")
        code = code.replace(old, new)
    require(code.count("/*ARGUS_POINT_STUDY*/") == 1, "Template must be blank.")
    require("fetch(" not in code and "playerX" not in code and "threatLocations" not in code,
            "Cannot expose agent guesses or upload human references.")
    payload = json.dumps(study, separators=(",", ":"), ensure_ascii=False).replace("<", "\\u003c")
    return code.replace("/*ARGUS_POINT_STUDY*/", payload)


def build(root: Path, output: Path) -> dict:
    require(not output.exists() or not any(output.iterdir()), "Refuse to replace reviewer or owner labels.")
    all_runs = []
    traces = set()
    approved = set()
    outputs = []
    for run in RUNS:
        directory = root / run
        require(directory.is_dir() and not directory.is_symlink(), "Development capture missing: " + run)
        require(not (directory / "labels.json").exists(), "Owner labels cannot enter source capture.")
        manifest = json.loads((directory / "run-manifest.json").read_text())
        require(manifest.get("schemaVersion") == 1 and manifest.get("partition") == "development"
                and manifest.get("runnerExitCode") == 0 and manifest.get("actualGameOutcome") == "unknown"
                and manifest.get("viewport") == "390x844" and
                manifest.get("visualEvidenceGate") in (True, False, None) and
                manifest.get("runId") == run,
                "Unrecognized capture / partition / outcome.")
        content = (directory / "trace.jsonl").read_bytes()
        digest = sha(content)
        require(re.fullmatch(r"[0-9a-f]{64}", digest) is not None and digest not in traces and
                manifest.get("traceSha256") == digest, "Duplicate or tampered whole-session capture.")
        traces.add(digest)
        rows = [json.loads(line) for line in content.splitlines()]
        require(36 <= len(rows) <= 180 and manifest.get("frames") == len(rows),
                "Need at least 36 truly captured source frames, bounded to 180.")
        require(all(row.get("step") == i and row.get("screenshot") == f"frame-{i:04d}.png"
                    for i, row in enumerate(rows)), "Source screenshot frame ordering changed.")
        steps = []
        for bin_id in range(SAMPLES):
            begin = bin_id * len(rows) // SAMPLES
            end = (bin_id + 1) * len(rows) // SAMPLES
            steps.append(begin + frozen_choice(run, bin_id, end - begin))
        require(len(steps) == len(set(steps)) == SAMPLES and steps == sorted(steps),
                "Chronological temporal-bin freeze invalid.")
        target = output / "annoteren" / run
        images = []
        for step in steps:
            filename = f"frame-{step:04d}.png"
            original = directory / filename
            require(original.is_file() and not original.is_symlink(), "Missing actual PNG.")
            raw = original.read_bytes()
            require(24 <= len(raw) <= 2_000_000 and raw.startswith(PNG) and
                    struct.unpack(">II", raw[16:24]) == (WIDTH, HEIGHT),
                    "Original frame is not a safe 390x844 PNG.")
            images.append({"step": step, "file": filename, "sha256": sha(raw)})
            outputs.append((target / filename, raw))
        phases = {}
        for step in steps:
            # Only a coarse analysis coverage report. NOT included in the blind reviewer.
            phase = rows[step].get("evidence", {}).get("phase", "unknown")
            phases[str(phase)] = phases.get(str(phase), 0) + 1
        study = {"schemaVersion": 1, "seed": SEED, "sourceSeed": SEED, "runId": run,
                 "partition": "development", "images": images}
        outputs.append((target / "index.html", reviewer_html(study).encode("utf-8")))
        all_runs.append({"runId": run, "partition": "development", "traceSha256": digest,
                         "runManifestSha256": sha((directory / "run-manifest.json").read_bytes()),
                         "originalFrames": len(rows), "selectionSteps": steps, "images": images,
                         "observedPhaseCountsDiagnosticOnly": phases,
                         "humanTruth": "ALL_UNKNOWN"})
        approved.update(images_i["sha256"] for images_i in images)
    require(len(traces) == len(RUNS), "Repeated browser traces cannot count as independent runs.")
    plan = {"schemaVersion": 1, "studySeed": SEED, "mode": "development-only",
            "selectionRule": "18 preregistered hashed chronological bins; NO detection/phase selection",
            "runIds": list(RUNS), "humanTruth": "ALL_UNKNOWN", "runs": all_runs,
            "publicPublicationAuthorized": False}
    outputs.append((output / "m220-study-plan.json", (json.dumps(plan, indent=2) + "\n").encode()))
    outputs.append((output / "index.html", (
        '<!doctype html><html lang="nl"><meta charset="utf-8"><meta name="viewport" '
        'content="width=device-width,initial-scale=1"><title>ARGUS M2.20</title>'
        '<body style="font:18px system-ui;max-width:600px;margin:auto;padding:18px">'
        '<h1>ARGUS M2.20 — privé ontwikkelonderzoek</h1>'
        '<p>Originele screenshots, alle spelerposities beginnen als ONBEKEND.</p>' +
        "".join(f'<p><a href="annoteren/{run}/">{run}: 18 blinde screenshots</a></p>'
                for run in RUNS) + "</body></html>").encode("utf-8")))
    for path, raw in outputs:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
    # Identical static screen PNGs can legitimately occur in multiple frames.
    # Report duplicates, but NEVER resample based on pixel content or agent guesses.
    plan["uniqueOriginalImageSha256Count"] = len(approved)
    (output / "m220-study-plan.json").write_text(json.dumps(plan, indent=2) + "\n")
    print(f"M2.20 FROZEN: 3 runs, {SAMPLES * len(RUNS)} distinct ORIGINAL PNGs, 0 human labels; no Pages publication.")
    return plan


def validate_owner(site: Path, label_files: list[Path]) -> dict:
    plan = json.loads((site / "m220-study-plan.json").read_text())
    require(plan.get("studySeed") == SEED and plan.get("mode") == "development-only"
            and plan.get("humanTruth") == "ALL_UNKNOWN" and plan.get("runIds") == list(RUNS),
            "Unknown frozen development study.")
    require(len(label_files) == 3, "Need 3 independent owner exports.")
    assigned = {}
    for path in label_files:
        require(path.is_file() and path.stat().st_size < 100_000, "Private human export missing/oversize.")
        obj = json.loads(path.read_text())
        run = obj.get("runId")
        require(run in RUNS and run not in assigned and set(obj) ==
                {"schemaVersion", "studySeed", "sourceStudySeed", "runId", "partition", "frames"} and
                obj["schemaVersion"] == 1 and obj["studySeed"] == SEED and
                obj["sourceStudySeed"] == SEED and obj["partition"] == "development",
                "Invalid or duplicate owner study export.")
        assigned[run] = obj
    require(set(assigned) == set(RUNS), "Missing owner run export.")
    stats = {}
    for run_spec in plan["runs"]:
        run = run_spec["runId"]
        samples = run_spec["images"]
        records = assigned[run]["frames"]
        require(isinstance(records, list) and len(records) == SAMPLES, "Incomplete human review.")
        counts = {"visible": 0, "absent": 0, "unknown": 0}
        far = 0
        for expected, actual in zip(samples, records):
            require(isinstance(actual, dict) and set(actual) ==
                    {"step", "screenshotSha256", "player"} and
                    actual["step"] == expected["step"] and
                    actual["screenshotSha256"] == expected["sha256"],
                    "Owner answer moved a frozen source image.")
            raw = (site / "annoteren" / run / expected["file"]).read_bytes()
            require(sha(raw) == expected["sha256"], "Source image differs from frozen review.")
            player = actual["player"]
            require(isinstance(player, dict) and set(player) == {"state", "center"} and
                    player["state"] in counts, "Unsupported owner state.")
            counts[player["state"]] += 1
            if player["state"] == "visible":
                c = player["center"]
                require(isinstance(c, dict) and set(c) == {"x", "y"} and
                        type(c["x"]) is int and type(c["y"]) is int and
                        0 <= c["x"] < WIDTH and 0 <= c["y"] < HEIGHT,
                        "Wrong player center pixel coordinate.")
                far += (c["x"] - 195)**2 + (c["y"] - 422)**2 >= 70**2
            else:
                require(player["center"] is None, "Absent/unknown truth has a center.")
        stats[run] = {"counts": counts, "offCenterVisible": far,
                      "readiness": "STRUCTURALLY_ELIGIBLE" if counts["visible"] >= 3 and
                      counts["absent"] >= 3 and far >= 2 else "NOT_READY"}
    return {"schemaVersion": 1, "studySeed": SEED,
            "status": ("STRUCTURALLY_ELIGIBLE" if all(s["readiness"] ==
                       "STRUCTURALLY_ELIGIBLE" for s in stats.values()) else "NOT_READY"),
            "runs": stats,
            "notice": "Only human-reviewed independent runs eligible. No model fitted; unreviewed=UNKNOWN; no game policy change."}


def main():
    p = argparse.ArgumentParser()
    sub = p.add_subparsers(dest="cmd", required=True)
    b = sub.add_parser("build")
    b.add_argument("--runs", type=Path, required=True)
    b.add_argument("--output", type=Path, required=True)
    v = sub.add_parser("audit")
    v.add_argument("--site", type=Path, required=True)
    for run in RUNS:
        v.add_argument("--labels-" + run[-1], type=Path, required=True)
    v.add_argument("--output", type=Path, required=True)
    opts = p.parse_args()
    if opts.cmd == "build":
        build(opts.runs, opts.output)
    else:
        report = validate_owner(opts.site, [opts.labels_i, opts.labels_j, opts.labels_k])
        opts.output.parent.mkdir(parents=True, exist_ok=True)
        opts.output.write_text(json.dumps(report, indent=2) + "\n")
        print("M2.20 private audit: " + report["status"] + " (no fitted model).")


if __name__ == "__main__":
    main()
