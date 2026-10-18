#!/usr/bin/env python3
"""Freeze new temporal dev R/S/T source pairs and BLANK mobile owner review.

PREDECLARED SHA-selected chronological 18 targets per independent real browser
process, plus original immediately previous screenshot for each target.
No guessed player, predicted class, game outcome, label, hidden telemetry, DOM
or sealed M2.23 O/P/Q file may influence source selection.
"""
import argparse
import hashlib
import json
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
RUNS = ("dev-r", "dev-s", "dev-t")
SEED = "argus-m227-temporal-context-rst-20261018-v1"
TARGETS = 18
PNG = b"\x89PNG\r\n\x1a\n"


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def demand(condition, explanation):
    if not condition:
        raise ValueError(explanation)


def blank_html(run, images):
    from tools.m220.point_cohort import TEMPLATE
    code = TEMPLATE.read_text(encoding="utf-8")
    pairs = [
        ('/^dev-[fgh]$/', '/^dev-[rst]$/'),
        ('study.images.length!==12', 'study.images.length!==18'),
        ('a.length===12', 'a.length===images.length'),
        ('idx===11', 'idx===images.length-1'),
        ('if(idx<11)', 'if(idx<images.length-1)'),
        ('" / 12 · "+', '" / "+images.length+" · "+'),
        ('"argus-m218-player-point-"', '"argus-m227-player-point-"'),
        ('"m218-"', '"m227-"'),
        ('ARGUS M2.18 · ', 'ARGUS M2.27 · ')
    ]
    for previous, updated in pairs:
        demand(code.count(previous) == 1, "Reviewer template drift: " + previous)
        code = code.replace(previous, updated)
    demand(code.count("/*ARGUS_POINT_STUDY*/") == 1 and "fetch(" not in code and
           "playerX" not in code and "threatLocations" not in code,
           "Private reference must not include agent/player guess or network upload.")
    study = {"schemaVersion": 1, "seed": SEED, "sourceSeed": SEED,
             "runId": run, "partition": "development", "images": images}
    return code.replace("/*ARGUS_POINT_STUDY*/", json.dumps(study, separators=(",", ":")))


def original_png(path):
    demand(path.is_file() and not path.is_symlink(), "Missing authentic screenshot.")
    data = path.read_bytes()
    demand(24 <= len(data) <= 2_000_000 and data.startswith(PNG) and
           struct.unpack(">II", data[16:24]) == (390, 844),
           "Source original PNG has wrong size/content.")
    return data


def freeze(root: Path, output: Path):
    demand(not output.exists() or not any(output.iterdir()),
           "Never overwrite a frozen owner review or earlier research.")
    files, runs, trace_shas, source_image_shas = [], [], set(), set()
    for run in RUNS:
        folder = root/run
        demand(folder.is_dir() and not folder.is_symlink(), "Missing independent game run.")
        meta_raw = (folder/"run-manifest.json").read_bytes()
        meta = json.loads(meta_raw)
        demand(meta.get("schemaVersion") == 1 and meta.get("runId") == run and
               meta.get("partition") == "development" and
               meta.get("runnerExitCode") == 0 and
               meta.get("viewport") == "390x844" and
               meta.get("visualEvidenceGate") is True and
               meta.get("actualGameOutcome") == "unknown",
               "Original real-game capture provenance failed.")
        raw_trace = (folder/"trace.jsonl").read_bytes()
        trace_sha = sha(raw_trace)
        demand(trace_sha == meta.get("traceSha256") and trace_sha not in trace_shas,
               "Original game trace changed or duplicate independent run.")
        trace_shas.add(trace_sha)
        lines = [json.loads(line) for line in raw_trace.splitlines()]
        demand(36 <= len(lines) <= 180 and meta.get("frames") == len(lines),
               "Too few independent original screenshot frames.")
        demand(all(x.get("step") == i and x.get("screenshot") == f"frame-{i:04d}.png"
                   for i, x in enumerate(lines)),
               "Original trace screenshot/step ordering changed.")
        selected, records, prior_records = [], [], []
        for bin_id in range(TARGETS):
            low = max(1, bin_id*len(lines)//TARGETS)
            high = (bin_id+1)*len(lines)//TARGETS
            demand(high > low, "Empty target time bin with no previous original frame.")
            digest = hashlib.sha256(f"{SEED}\n{run}\n{bin_id}".encode()).digest()
            step = low + int.from_bytes(digest[:8], "big") % (high-low)
            previous_step = step - 1
            filename = f"frame-{step:04d}.png"
            prior_name = f"prev-for-{step:04d}.png"
            target_raw = original_png(folder/filename)
            previous_raw = original_png(folder/f"frame-{previous_step:04d}.png")
            target_sha, previous_sha = sha(target_raw), sha(previous_raw)
            selected.append(step)
            source_image_shas.add(target_sha)
            records.append({"step": step, "file": filename, "sha256": target_sha})
            prior_records.append({"targetStep": step, "priorStep": previous_step,
                                  "file": f"context/{run}/{prior_name}",
                                  "sha256": previous_sha})
            files.append((output/"annoteren"/run/filename, target_raw))
            files.append((output/"context"/run/prior_name, previous_raw))
        demand(selected == sorted(set(selected)) and len(selected) == TARGETS,
               "Nonchronological sampled original frames.")
        runs.append({"runId": run, "totalSourceFrames": len(lines),
                     "sourceTraceSha256": trace_sha, "sourceManifestSha256": sha(meta_raw),
                     "images": records, "immediatePriorOriginalFrames": prior_records})
        files.append((output/"annoteren"/run/"index.html", blank_html(run, records).encode()))
    demand(len(trace_shas) == 3, "Need three distinct whole-browser source traces.")
    manifest = {"schemaVersion": 1, "seed": SEED,
                "sourceRole": "development-temporal-only", "runIds": list(RUNS),
                "targetsPerRun": TARGETS, "originalTargetImages": 54,
                "originalPrecedingContextImages": 54,
                "uniqueTargetImageDigests": len(source_image_shas),
                "selectionRule": "18 SHA-seeded chronological bins/run, target step >=1, immediate prior ORIGINAL frame; label/phase/outcome-blind",
                "humanGroundTruth": "ALL_UNKNOWN", "trainingAuthorized": True,
                "prospectiveOriginalsAccessed": False, "runs": runs}
    files.append((output/"study-manifest.json", (json.dumps(manifest, indent=2)+"\n").encode()))
    homepage = '<!doctype html><html lang="nl"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>ARGUS M2.27</title><body><h1>ARGUS M2.27 · nieuwe ontwikkelreview</h1>'
    homepage += "".join(f'<p><a href="annoteren/{run}/">{run}: 18 oorspronkelijke screenshots</a></p>' for run in RUNS)
    files.append((output/"index.html", (homepage+"</body></html>").encode()))
    for path, data in files:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    demand(len(list(output.rglob("*.png"))) == 108 and
           len(list(output.rglob("*.json"))) == 1 and
           len(list(output.rglob("*.jsonl"))) == 0 and
           len(list(output.rglob("annoteren/*/index.html"))) == 3,
           "Unexpected output files: private truth, source traces or prospective images.")
    print("PASS M2.27: three independent game browser runs, 54 frozen review screenshots, "
          "54 original preceding contexts, zero human guesses, no prospective access.")
    return manifest


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--runs", type=Path, required=True)
    p.add_argument("--output", type=Path, required=True)
    a = p.parse_args()
    freeze(a.runs, a.output)


if __name__ == "__main__":
    main()
