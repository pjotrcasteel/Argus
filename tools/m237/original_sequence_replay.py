#!/usr/bin/env python3
"""M2.37 private, original-PNG-only, game-neutral causal identity replay.

All reports and source images remain LOCAL. This tool does not control games,
read their DOM, verify a player, or publish private model weights.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path

import numpy as np
from PIL import Image

from tools.m230 import identity_study as appearance
from tools.m235.causal_identity import (
    CausalIdentityResearch, MIN_SCREEN_PIXEL_CHANGE, frame_hash,
)
from tools.m236 import contrastive_identity as contrast

MODEL_ID = "m237-private-causal-appearance-original-replay-v1"
MAX_LOCATION_ERROR = 35
MIN_VISIBLE = 8
MIN_ABSENT = 3
MIN_NEW_RUNS = 3
MIN_HIT_RATE = .50
MAX_FALSE_PRESENCE = .25
# A cut-off image patch is not sufficient to claim the complete identity.
BORDER_REJECT_PX = 24
HISTORICAL_RUNS = frozenset((
    "run-01", "dev-o", "dev-p", "dev-q", "dev-r", "dev-s",
    "dev-t", "dev-u", "dev-v", "dev-w",
))


def require(condition, reason):
    if not condition:
        raise ValueError(reason)


def _sha(value):
    return hashlib.sha256(value).hexdigest()


def _read_rgb(root, entry):
    path = entry.get("path")
    digest = entry.get("sha256")
    require(type(path) is str and path.endswith(".png") and
            type(digest) is str and len(digest) == 64 and
            all(x in "0123456789abcdef" for x in digest),
            "Original PNG relative path and lowercase byte SHA-256 required.")
    relative = Path(path)
    require(not relative.is_absolute() and ".." not in relative.parts and
            not any(part.startswith(".") for part in relative.parts),
            "Refuse escaped or hidden private screenshot path.")
    source = root / relative
    require(source.is_file() and not any(p.is_symlink()
            for p in (source, *source.parents) if p != root.parent),
            "Original screenshot must be a local regular non-symlink file.")
    raw = source.read_bytes()
    require(_sha(raw) == digest, "Original PNG bytes do not match immutable source SHA.")
    try:
        with Image.open(io.BytesIO(raw)) as image:
            require(image.format == "PNG" and image.mode == "RGB" and
                    image.size == (390, 844) and image.n_frames == 1,
                    "Original screenshot must be one untouched 390x844 RGB PNG.")
            pixels = np.asarray(image, dtype=np.uint8).copy()
    except (OSError, SyntaxError) as exc:
        raise ValueError("Original screenshot cannot be decoded as PNG.") from exc
    frame_hash(pixels)
    return pixels


def _source_rows(root, records, partition, seen):
    require(type(records) is list and records, "Source partition must not be empty.")
    rows = []
    for record in records:
        require(type(record) is dict and type(record.get("run")) is str and
                record["run"] and type(record.get("step")) is int and
                record["step"] >= 0, "Original row requires run and chronological step.")
        digest = record.get("sha256")
        require(digest not in seen, "Duplicate original SHA across source partitions/runs.")
        rgb = _read_rgb(root, record)
        require(record["sha256"] not in seen, "Original screenshot source reused.")
        seen.add(record["sha256"])
        row = dict(run=record["run"], step=record["step"],
                   sha=record["sha256"], rgb=rgb)
        if partition != "replay":
            state, center = record.get("state"), record.get("center")
            require(state in ("visible", "absent") and
                    ((state == "absent" and center is None) or
                     (state == "visible" and type(center) is dict and
                      type(center.get("x")) is int and type(center.get("y")) is int and
                      0 <= center["x"] < 390 and 0 <= center["y"] < 844)),
                    "Only reviewed visible-center or whole-ABSENT reference accepted.")
            row.update(state=state, center=center)
        else:
            require(not any(k in record for k in ("state", "center", "prediction")),
                    "Replay inputs must not expose human truth or model predictions.")
        rows.append(row)
    return rows


def admit_original_manifest(manifest, root):
    """Strict source/run/label separation BEFORE fitting any image features."""
    require(type(manifest) is dict and manifest.get("schemaVersion") == 1 and
            manifest.get("studyType") in ("DEVELOPMENT_DIAGNOSTIC", "NEW_SOURCE") and
            type(manifest.get("runs")) is list and manifest["runs"],
            "M2.37 frozen private source manifest invalid.")
    # Refuse sealed source identifiers BEFORE opening even one PNG byte.
    # A late check would leak protected originals into the research process.
    preflight = list(manifest.get("fit", [])) + list(manifest.get("calibration", []))
    names = {row.get("run") for row in preflight if type(row) is dict}
    eval_ids = {part.get("runId") for part in manifest["runs"] if type(part) is dict}
    require(not ({"run-01", "dev-o", "dev-p", "dev-q"} & (names | eval_ids)),
            "SEALED: original run-01 and O/P/Q must never be opened.")
    if manifest["studyType"] == "NEW_SOURCE":
        require(len(manifest["runs"]) >= MIN_NEW_RUNS and
                not (eval_ids & HISTORICAL_RUNS),
                "NOT_READY: >=3 genuinely new source sessions required.")
    seen = set()
    fit = _source_rows(root, manifest.get("fit"), "fit", seen)
    cal = _source_rows(root, manifest.get("calibration"), "calibration", seen)
    require(len({r["run"] for r in fit}) >= 2 and
            len({r["run"] for r in cal}) >= 2 and
            not ({r["run"] for r in fit} & {r["run"] for r in cal}),
            "NOT_READY: fit and whole-ABSENT calibration require disjoint source runs.")
    require(len(cal) >= 4 and all(r["state"] == "absent" for r in cal),
            "NOT_READY: >=4 human-ABSENT whole originals required for calibration.")
    evaluation = {}
    for part in manifest["runs"]:
        require(type(part) is dict and type(part.get("runId")) is str and
                part["runId"] and part["runId"] not in evaluation,
                "Evaluation run IDs must be independent and unique.")
        run = part["runId"]
        frames = _source_rows(root, part.get("frames"), "replay", seen)
        require(all(r["run"] == run for r in frames) and
                all(a["step"] < b["step"] for a, b in zip(frames, frames[1:])),
                "Original sequence must be strictly ordered; never sort or interpolate.")
        evaluation[run] = frames
    require(not (set(evaluation) & ({r["run"] for r in fit} | {r["run"] for r in cal})),
            "NOT_READY: evaluation runs must be wholly disjoint from fit/calibration.")
    require(not ({"run-01", "dev-o", "dev-p", "dev-q"} &
                (set(evaluation) | {r["run"] for r in fit + cal})),
            "SEALED: original run-01 and O/P/Q cannot be used in M2.37.")
    if manifest["studyType"] == "NEW_SOURCE":
        require(not (set(evaluation) & HISTORICAL_RUNS) and
                len(evaluation) >= MIN_NEW_RUNS,
                "NOT_READY: >=3 genuinely new run IDs, no previously inspected runs.")
    return fit, cal, evaluation


def replay_originals(manifest, root, *, grid=appearance.GRID):
    fit, cal, evaluation = admit_original_manifest(manifest, root)
    model = contrast.fit_metric(fit)
    threshold = contrast.calibrate_absent(cal, model, grid=grid)
    digest = contrast.model_digest(model, threshold)
    runs = {}
    for run, frames in evaluation.items():
        tracker = CausalIdentityResearch()
        results = []
        previous = None
        for row in frames:
            # Frame zero and chronological gaps have NO legitimate predecessor.
            paired = (previous is not None and
                      previous["step"] + 1 == row["step"] and
                      previous["sha"] != row["sha"])
            prior_rgb = previous["rgb"] if paired else row["rgb"]
            proposals = contrast.source_candidates(prior_rgb, row["rgb"], model,
                                                   grid=grid)
            temporal = tracker.update(run, row["step"], row["sha"], proposals,
                                      previous_sha=previous["sha"] if paired else None)
            appearance_vote = contrast.classify_shadow(proposals, threshold)
            causal_point = temporal["candidate"]
            visual_point = appearance_vote["candidate"]
            agrees = (causal_point is not None and visual_point is not None and
                      all(causal_point[k] == visual_point[k] for k in ("x", "y")))
            accepted = (paired and agrees and
                        temporal["status"] == "UNVERIFIED_SOURCE_ONLY_IDENTITY_HYPOTHESIS" and
                        appearance_vote["status"] == "UNVERIFIED_APPEARANCE_CANDIDATE" and
                        BORDER_REJECT_PX <= visual_point["x"] < 390-BORDER_REJECT_PX and
                        BORDER_REJECT_PX <= visual_point["y"] < 844-BORDER_REJECT_PX and
                        any(c.x == visual_point["x"] and c.y == visual_point["y"] and
                            c.raw_change >= MIN_SCREEN_PIXEL_CHANGE for c in proposals))
            results.append({
                "step": row["step"], "sha256": row["sha"],
                "status": "SHADOW_ACCEPTED_HYPOTHESIS" if accepted else "ABSTAIN",
                "candidate": visual_point if accepted else None,
                "appearanceStatus": appearance_vote["status"],
                "causalStatus": temporal["status"],
                "consecutiveOriginalFrames": bool(temporal["consecutiveOriginalFrames"]),
                "playerPresent": None, "verifiedPlayerCenter": None,
            })
            previous = row
        runs[run] = results
    return {"schemaVersion": 1, "method": MODEL_ID,
            "studyType": manifest["studyType"],
            "gameVersion": manifest.get("gameVersion", "unknown"),
            "sourceType": "PRIVATE_ORIGINAL_PNG_ONLY",
            "frozenModelSha256": digest,
            "fitRuns": len({r["run"] for r in fit}),
            "calibrationAbsentOriginals": len(cal),
            "runs": runs, "status": "NOT_EVALUATED",
            "liveAgentChanged": False, "playerIdentityVerified": False}


def evaluate_private_review(replay, labels):
    """Scoring uses independent future reviewed originals, never training truth.

    Labels must cover ALL replay frames; UNKNOWN is not an ABSENT negative.
    A hit only measures a SHADOW hypothesis within pixel radius, never
    authenticates identity or permits the live agent to consume it.
    """
    require(type(labels) is dict and type(labels.get("runs")) is list,
            "Independently human-reviewed original-frame labels required.")
    reviews = {}
    for run in labels["runs"]:
        require(type(run) is dict and type(run.get("runId")) is str and
                run["runId"] not in reviews and type(run.get("frames")) is list,
                "Private human reference run malformed.")
        reviews[run["runId"]] = run["frames"]
    require(set(reviews) == set(replay["runs"]),
            "Incomplete or extra reviewed run; reject selection changes.")
    metrics = {}
    for run_id, frames in replay["runs"].items():
        reference = reviews[run_id]
        require(len(reference) == len(frames), "Every original replay frame must be reviewed.")
        m = dict(visible=0, absent=0, unknown=0, within35=0,
                 falsePresence=0, wrongLocation=0, missedVisible=0)
        for found, truth in zip(frames, reference):
            require(type(truth) is dict and truth.get("step") == found["step"] and
                    truth.get("sha256") == found["sha256"] and
                    truth.get("state") in ("visible", "absent", "unknown"),
                    "Human references must match frozen original step and SHA.")
            state, center = truth["state"], truth.get("center")
            require((state != "visible" and center is None) or
                    (state == "visible" and type(center) is dict and
                     type(center.get("x")) is int and type(center.get("y")) is int and
                     0 <= center["x"] < 390 and 0 <= center["y"] < 844),
                    "Invalid independently reviewed center or ABSENT/UNKNOWN semantics.")
            pred = found["candidate"]
            if state == "unknown":
                m["unknown"] += 1
            elif state == "absent":
                m["absent"] += 1
                m["falsePresence"] += int(pred is not None)
            else:
                m["visible"] += 1
                if pred is None:
                    m["missedVisible"] += 1
                elif (pred["x"]-center["x"])**2 + (pred["y"]-center["y"])**2 <= MAX_LOCATION_ERROR**2:
                    m["within35"] += 1
                else:
                    m["wrongLocation"] += 1
        enough = m["visible"] >= MIN_VISIBLE and m["absent"] >= MIN_ABSENT
        m["status"] = ("NOT_READY" if not enough else
                       "DIAGNOSTIC_ONLY" if replay["studyType"] != "NEW_SOURCE" else
                       "PRELIMINARY_GO" if m["within35"]/m["visible"] >= MIN_HIT_RATE and
                       m["falsePresence"]/m["absent"] <= MAX_FALSE_PRESENCE else "NO_GO")
        metrics[run_id] = m
    overall = ("PRELIMINARY_GO_RESEARCH_ONLY" if replay["studyType"] == "NEW_SOURCE" and
               len(metrics) >= MIN_NEW_RUNS and
               all(m["status"] == "PRELIMINARY_GO" for m in metrics.values()) else
               "NOT_READY" if any(m["status"] == "NOT_READY" for m in metrics.values())
               else "NO_GO_OR_DIAGNOSTIC")
    return {"schemaVersion": 1, "status": overall,
            "humanReviewProvenance": "EXTERNAL_TO_TOOL_NOT_AUTHENTICATED",
            "runMetrics": metrics, "liveAgentChanged": False,
            "playerIdentityVerified": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--human-labels", type=Path)
    args = parser.parse_args()
    require(args.output != args.manifest and not args.output.exists() and
            (args.human_labels is None or args.output != args.human_labels),
            "Refuse to overwrite input or an existing private evaluation report.")
    manifest = json.loads(args.manifest.read_text())
    report = replay_originals(manifest, args.manifest.parent)
    if args.human_labels is not None:
        report["evaluation"] = evaluate_private_review(
            report, json.loads(args.human_labels.read_text()))
        report["status"] = report["evaluation"]["status"]
    args.output.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
    print(report["status"], "|", len(report["runs"]), "private original whole runs | no live agent changes")


if __name__ == "__main__":
    main()
