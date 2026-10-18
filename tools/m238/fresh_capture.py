#!/usr/bin/env python3
"""M2.38: independently capture and freeze post-update, original-only development.

Only external-browser PNG, ordinary inputs and trace metadata are admissible.
No owner labels, model predictions, DOM state or game engine internals enter
selection or publication. Captures stay in short-lived private Actions artifacts.
"""
import argparse
import datetime
import hashlib
import json
from pathlib import Path

from PIL import Image

from tools.m232 import freeze_new_development as previous_review

RUNS = ("m238-a", "m238-b", "m238-c")
URL = "https://pjotrcasteel.github.io/Wyrmforge/"
SEED = "argus-m238-post-wyrmforge-121-20261018-original-v1"
TARGETS = 18
MIN_FRAMES = 36
MAX_FRAMES = 180
PNG_LIMIT = 2_000_000


def require(ok, why):
    if not ok:
        raise ValueError(why)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def real_png(path):
    require(path.is_file() and not path.is_symlink(), "Missing non-symlink original PNG.")
    raw = path.read_bytes()
    require(24 <= len(raw) <= PNG_LIMIT, "Original screenshot invalid size.")
    with Image.open(path) as im:
        require(im.format == "PNG" and im.mode == "RGB" and im.size == (390, 844)
                and getattr(im, "n_frames", 1) == 1,
                "Only authentic 390x844 original RGB screenshot accepted.")
        im.verify()
    return raw


def record(folder, run, started, runner_exit, argus_revision, profile):
    require(run in RUNS and type(runner_exit) is int and runner_exit >= 0,
            "Cannot record fabricated or out-of-scope runner process.")
    require(profile.is_file(), "Profile file missing.")
    trace = folder / "trace.jsonl"
    report = folder / "summary.json"
    raw_trace = trace.read_bytes() if trace.is_file() else b""
    summary = json.loads(report.read_text()) if report.is_file() else {}
    meta = {
        "schemaVersion": 1, "partition": "new-development-not-prospective-claims",
        "runId": run, "captureKind": "real independent public visual browser",
        "gameUrl": URL, "gameVersion": None,
        "versionAttestedByPublicGame": False,
        "startedAtUtc": started,
        "finishedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "runnerExitCode": runner_exit,
        "argusRevision": argus_revision,
        "viewport": "390x844", "actualOutcome": "unknown",
        "profileSha256": sha(profile.read_bytes()),
        "traceSha256": sha(raw_trace) if raw_trace else None,
        "frames": summary.get("frames"),
        "visualEvidenceGate": summary.get("meetsVisualCombatGate"),
        "captureBasedOnPredictions": False,
    }
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "run-manifest.json").write_text(json.dumps(meta, indent=2) + "\n")
    return meta


def verify_run(folder, run):
    require(run in RUNS and folder.is_dir() and not folder.is_symlink(),
            "Missing independent whole-run source folder.")
    metadata = folder / "run-manifest.json"
    trace = folder / "trace.jsonl"
    require(metadata.is_file() and trace.is_file(), "Original browser trace/manifest missing.")
    meta = json.loads(metadata.read_text())
    raw = trace.read_bytes()
    rows = [json.loads(line) for line in raw.splitlines()]
    require(meta.get("schemaVersion") == 1 and meta.get("runId") == run and
            meta.get("runnerExitCode") == 0 and meta.get("gameUrl") == URL and
            meta.get("viewport") == "390x844" and
            meta.get("actualOutcome") == "unknown" and
            meta.get("gameVersion") is None and
            meta.get("versionAttestedByPublicGame") is False and
            meta.get("visualEvidenceGate") is True and
            meta.get("captureBasedOnPredictions") is False and
            meta.get("traceSha256") == sha(raw) and
            meta.get("frames") == len(rows) and
            MIN_FRAMES <= len(rows) <= MAX_FRAMES,
            "NOT_READY: real independent run, public visual gate or unknown version not verified.")
    image_hashes = []
    for idx, row in enumerate(rows):
        filename = f"frame-{idx:04d}.png"
        require(row.get("step") == idx and row.get("screenshot") == filename,
                "Trace step and original PNG order inconsistent.")
        image_hashes.append(sha(real_png(folder / filename)))
    require(meta.get("profileSha256") and len(meta["profileSha256"]) == 64 and
            meta.get("argusRevision") and
            meta.get("startedAtUtc") and meta.get("finishedAtUtc"),
            "Missing actual browser source provenance.")
    return {"runId": run, "frames": len(rows), "traceSha256": sha(raw),
            "originalPngSha256": image_hashes, "meta": meta}


def blank_reviewer(run, images):
    # Reuse already real-mobile-tested offline review UI; no new disclosure.
    html = previous_review.blank_html("dev-u", images)
    require(html.count("/^dev-[uvw]$/") == 1 and
            html.count('"runId":"dev-u"') == 1,
            "Existing mobile labeler contract changed; refuse broken review.")
    html = html.replace("/^dev-[uvw]$/", "/^m238-[abc]$/")
    html = html.replace('"runId":"dev-u"', '"runId":"' + run + '"')
    html = html.replace("M2.32", "M2.38").replace("m232-", "m238-")
    require(run in html and "fetch(" not in html and
            '"playerX"' not in html and '"threatLocations"' not in html,
            "Reviewer must remain independent of predictions or network.")
    return html


def freeze(runs_root, output):
    require(not output.exists() or not any(output.iterdir()),
            "Never replace an existing original review freeze.")
    admitted = [verify_run(runs_root / run, run) for run in RUNS]
    require(len({r["traceSha256"] for r in admitted}) == len(RUNS),
            "Duplicate whole browser traces do not establish independence.")
    files, experiments, selected_hashes = [], [], set()
    for record in admitted:
        run = record["runId"]
        folder = runs_root / run
        count = record["frames"]
        selected, images, context = [], [], []
        for bin_no in range(TARGETS):
            low = max(1, bin_no * count // TARGETS)
            high = (bin_no+1) * count // TARGETS
            require(high > low, "Empty original sampling time bin.")
            signature = sha((SEED + "\n" + run + "\n" + str(bin_no)).encode())
            step = low + int(signature[:16], 16) % (high-low)
            original_name = f"frame-{step:04d}.png"
            prior_name = f"prev-for-{step:04d}.png"
            original = real_png(folder / original_name)
            prior = real_png(folder / f"frame-{step-1:04d}.png")
            original_sha = sha(original)
            selected_hashes.add(original_sha)
            selected.append(step)
            images.append({"step": step, "file": original_name, "sha256": original_sha})
            context.append({"targetStep": step, "priorStep": step-1,
                            "file": f"context/{run}/{prior_name}", "sha256": sha(prior)})
            files.append((output / "annoteren" / run / original_name, original))
            files.append((output / "context" / run / prior_name, prior))
        require(selected == sorted(set(selected)) and len(selected) == TARGETS,
                "Frozen original chronological sampling invalid.")
        experiments.append({
            "runId": run, "sourceTraceSha256": record["traceSha256"],
            "sourceManifestSha256": sha((folder / "run-manifest.json").read_bytes()),
            "frames": record["frames"], "images": images,
            "immediatePriorOriginalFrames": context,
            "gameVersion": None, "humanGroundTruth": "ALL_UNKNOWN",
        })
        files.append((output / "annoteren" / run / "index.html",
                      blank_reviewer(run, images).encode()))
    manifest = {
        "schemaVersion": 1, "study": "M2.38_NEW_EXTERNAL_DEVELOPMENT",
        "researchStatus": "NOT_EVALUATED",
        "gameUrl": URL, "gameVersion": None, "versionAttested": False,
        "sourceSeed": SEED, "runIds": list(RUNS),
        "sampledFramesPerRun": TARGETS,
        "originalTargetCount": len(RUNS) * TARGETS,
        "originalImmediatePredecessorCount": len(RUNS) * TARGETS,
        "uniqueOriginalTargetDigests": len(selected_hashes),
        "selectionRule": "SHA-seeded chronological time bins, never predictions/labels",
        "ownerGroundTruth": "NONE", "prospectiveSealedSourcesAccessed": False,
        "publicPublicationAuthorized": False,
        "runs": experiments,
    }
    files.append((output / "study-manifest.json",
                  (json.dumps(manifest, indent=2) + "\n").encode()))
    index = ("<!doctype html><html lang='en'><meta charset='utf-8'>"
             "<title>ARGUS M2.38 private blind source review</title><body>"
             "<h1>M2.38 · New original game frames (private)</h1>"
             "<p>Review screenshots without seeing ARGUS player predictions.</p>" +
             "".join(f"<p><a href='annoteren/{r}/'>{r}: {TARGETS} originals</a></p>"
                     for r in RUNS) + "</body></html>")
    files.append((output / "index.html", index.encode()))
    for path, raw in files:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
    require(len(list(output.rglob("*.png"))) == 2 * TARGETS * len(RUNS) and
            len(list(output.rglob("*.json"))) == 1 and
            not list(output.rglob("labels.json")) and
            not list(output.rglob("*.jsonl")),
            "No raw traces, private reviewer labels or non-selected screenshots may leak.")
    return manifest


def main():
    p = argparse.ArgumentParser(description=__doc__)
    commands = p.add_subparsers(dest="cmd", required=True)
    rec = commands.add_parser("record")
    rec.add_argument("--folder", required=True, type=Path)
    rec.add_argument("--run", required=True, choices=RUNS)
    rec.add_argument("--started", required=True)
    rec.add_argument("--exit", required=True, type=int)
    rec.add_argument("--revision", required=True)
    rec.add_argument("--profile", required=True, type=Path)
    ver = commands.add_parser("verify")
    ver.add_argument("--runs", required=True, type=Path)
    f = commands.add_parser("freeze")
    f.add_argument("--runs", required=True, type=Path)
    f.add_argument("--output", required=True, type=Path)
    args = p.parse_args()
    if args.cmd == "record":
        print(json.dumps(record(args.folder, args.run, args.started, args.exit,
                                args.revision, args.profile)))
    elif args.cmd == "verify":
        sources = [verify_run(args.runs / run, run) for run in RUNS]
        require(len({r["traceSha256"] for r in sources}) == len(RUNS),
                "Not three independent original traces.")
        print("PASS 3 bounded authentic external browser sources:",
              [(r["runId"], r["frames"]) for r in sources])
    else:
        print("Frozen original private study:",
              json.dumps(freeze(args.runs, args.output), indent=2))


if __name__ == "__main__":
    main()
