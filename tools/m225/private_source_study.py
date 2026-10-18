#!/usr/bin/env python3
"""M2.25: genuine PRIVATE owner-reference admission and preregistered offline study.

Pass only original development L/M/N ZIP and local human exports. NEVER access
sealed prospective O/P/Q, original M2.4 run-01, network, game source/DOM or
runtime player steering. No owner input or trained weights in public CI/artifacts.

This file does not set/tune any M2.23 model parameters: training code comes
unchanged from preregistered tools.m223.multiscale_appearance.
"""
import argparse
import hashlib
import json
import re
import struct
import sys
from collections import Counter
from io import BytesIO
from pathlib import Path
from zipfile import ZipFile

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
from tools.m223 import multiscale_appearance as learner
from tools.m223.freeze_new_cohorts import DEVELOPMENT, SEEDS

SOURCE_ARCHIVE_SHA256 = "400b9df3233ab0cf1af20d2e57d9cef84ffb48bbdd7a40f98bc4d4700f1c303d"
SOURCE_SEED = SEEDS["development"]
ORIGINAL_WIDTH = 390
ORIGINAL_HEIGHT = 844
EXPECTED_PER_RUN = 18
MIN_VISIBLE = 3
MIN_ABSENT = 3
MIN_OFF_CENTER = 2
CENTER = (195, 422)
OFF_CENTER_RADIUS = 70
PNG = b"\x89PNG\r\n\x1a\n"


def reject_unless(predicate, message):
    if not predicate:
        raise ValueError(message)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def canonical(obj):
    return json.dumps(obj, sort_keys=True, separators=(",", ":"), allow_nan=False)


def validate_and_load(archive: Path, labels: list[Path]):
    reject_unless(archive.is_file() and archive.stat().st_size < 24_000_000 and
                  sha(archive.read_bytes()) == SOURCE_ARCHIVE_SHA256,
                  "Original M2.23 development ZIP SHA256 differs: FAIL CLOSED.")
    reject_unless(len(labels) == len(DEVELOPMENT), "Expected exactly three private owner exports.")
    private = {}
    for path in labels:
        reject_unless(path.is_file() and path.stat().st_size < 100_000,
                      "Owner file missing or unexpected oversized.")
        obj = json.loads(path.read_text(encoding="utf-8"))
        run = obj.get("runId")
        reject_unless(run in DEVELOPMENT and run not in private and
                      set(obj) == {"schemaVersion", "studySeed", "sourceStudySeed",
                                   "runId", "partition", "frames"} and
                      obj["schemaVersion"] == 1 and obj["studySeed"] == SOURCE_SEED and
                      obj["sourceStudySeed"] == SOURCE_SEED and
                      obj["partition"] == "development" and
                      isinstance(obj["frames"], list) and
                      len(obj["frames"]) == EXPECTED_PER_RUN,
                      "Owner study/run/schema mismatch or duplicate owner export.")
        private[run] = obj
    reject_unless(set(private) == set(DEVELOPMENT), "Missing private independent run.")
    by_run, coverage, seen_source_sha, seen_human_truth = {}, {}, set(), {}
    with ZipFile(archive) as original:
        names = original.namelist()
        reject_unless(len(names) == len(set(names)) == 59 and
                      all(not n.startswith("/") and ".." not in Path(n).parts and
                          not n.endswith("/") for n in names) and
                      original.testzip() is None, "Frozen original development archive invalid.")
        study = json.loads(original.read("study-manifest.json"))
        reject_unless(study.get("schemaVersion") == 1 and
                      study.get("seed") == SOURCE_SEED and
                      study.get("sourceRole") == "development" and
                      study.get("partition") == "development" and
                      study.get("runIds") == list(DEVELOPMENT) and
                      study.get("numOriginalImages") == EXPECTED_PER_RUN * 3 and
                      study.get("humanGroundTruth") == "ALL_UNKNOWN" and
                      study.get("trainingAuthorized") is True and
                      study.get("prospectiveSealIntact") is False and
                      len(study.get("runs", [])) == 3,
                      "Not authorized, frozen original L/M/N development source.")
        allowed = {"study-manifest.json", "index.html"}
        for source_run in study["runs"]:
            run = source_run.get("runId")
            reject_unless(run in DEVELOPMENT and run not in by_run and
                          len(source_run.get("images", [])) == EXPECTED_PER_RUN and
                          re.fullmatch("[a-f0-9]{64}", source_run.get("sourceTraceSha256", "")) and
                          re.fullmatch("[a-f0-9]{64}", source_run.get("sourceManifestSha256", "")),
                          "Incorrect run provenance or duplicate source run.")
            allowed.add(f"annoteren/{run}/index.html")
            html = original.read(f"annoteren/{run}/index.html").decode("utf-8")
            reject_unless(html.count("<script>") == html.count("</script>") == 1 and
                          '"state":"visible"' not in html and
                          '"argus-m223-player-point-"' in html and "fetch(" not in html,
                          "Original blank reviewer contains unexpected public human truth.")
            rows, counters, off = [], Counter(), 0
            previous_step = -1
            for expected, actual in zip(source_run["images"], private[run]["frames"]):
                step = expected.get("step")
                name = expected.get("file")
                digest = expected.get("sha256")
                reject_unless(type(step) is int and previous_step < step <= 180 and
                              name == f"frame-{step:04d}.png" and
                              re.fullmatch("[a-f0-9]{64}", digest or "") is not None,
                              "Source frame step/SHA changed or ordering incorrect.")
                previous_step = step
                reject_unless(isinstance(actual, dict) and
                              set(actual) == {"step", "screenshotSha256", "player"} and
                              actual["step"] == step and
                              actual["screenshotSha256"] == digest,
                              "Owner answer does not match frozen original frame/SHA.")
                path = f"annoteren/{run}/{name}"
                allowed.add(path)
                raw = original.read(path)
                reject_unless(len(raw) >= 24 and raw.startswith(PNG) and
                              struct.unpack(">II", raw[16:24]) == (ORIGINAL_WIDTH, ORIGINAL_HEIGHT)
                              and sha(raw) == digest, "Original PNG bytes/dimensions/SHA mismatch.")
                player = actual["player"]
                reject_unless(isinstance(player, dict) and
                              set(player) == {"state", "center"} and
                              player["state"] in ("visible", "absent", "unknown"),
                              "Invalid VISIBLE/ABSENT/UNKNOWN human state.")
                state, center = player["state"], player["center"]
                if state == "visible":
                    reject_unless(isinstance(center, dict) and set(center) == {"x", "y"} and
                                  type(center["x"]) is int and type(center["y"]) is int and
                                  0 <= center["x"] < ORIGINAL_WIDTH and
                                  0 <= center["y"] < ORIGINAL_HEIGHT,
                                  "Visible center missing or outside source screenshot.")
                    off += int((center["x"] - CENTER[0])**2 +
                               (center["y"] - CENTER[1])**2 >= OFF_CENTER_RADIUS**2)
                else:
                    reject_unless(center is None, "Absent/unknown image cannot carry a player center.")
                truth_key = (state, tuple(sorted(center.items())) if center else None)
                if digest in seen_human_truth:
                    reject_unless(seen_human_truth[digest] == truth_key,
                                  "Conflicting human labels for byte-identical source PNG.")
                else:
                    seen_human_truth[digest] = truth_key
                counters[state] += 1
                with Image.open(BytesIO(raw)) as image:
                    pixels = np.asarray(image.convert("RGB"), dtype=np.uint8)
                reject_unless(pixels.shape == (ORIGINAL_HEIGHT, ORIGINAL_WIDTH, 3),
                              "Original pixel decode invalid.")
                rows.append({"run": run, "step": step, "sha": digest, "state": state,
                             "center": center, "rgb": pixels})
                seen_source_sha.add(digest)
            reject_unless(counters["visible"] >= MIN_VISIBLE and
                          counters["absent"] >= MIN_ABSENT and off >= MIN_OFF_CENTER,
                          "NOT_READY: owner class/position per-whole-run reference gates failed.")
            by_run[run] = rows
            coverage[run] = {"visible": counters["visible"], "absent": counters["absent"],
                             "unknown": counters["unknown"], "offCenterVisible": off,
                             "sourceFrames": source_run["totalOriginalFrames"]}
        reject_unless(set(by_run) == set(DEVELOPMENT) and set(names) == allowed,
                      "Unexpected prospective, private export, model or raw trace in source archive.")
    return by_run, coverage, len(seen_source_sha)


def model_pack(model):
    """ONLY private destination. JSON must NEVER be committed or uploaded as artifact."""
    return {"schemaVersion": 1, "featureDimension": int(model.weights.size),
            "mean": model.mean.tolist(), "std": model.std.tolist(),
            "weights": model.weights.tolist(), "bias": model.bias,
            "absentThreshold": model.absent_threshold,
            "trainingPositivePatches": model.train_positive_patches,
            "trainingNegativePatches": model.train_negative_patches,
            "humanAbsentCalibrationImages": model.train_absent_frames}


def evaluate_with_locked_parameters(by_run, coverage, distinct):
    """Exactly the frozen M2.23 algorithm. No hyperparameter or test-fold tuning."""
    folds = []
    for fold in learner.independent_folds(by_run):
        # Score only after train()/absent calibration has consumed training runs.
        fit = learner.train(fold["fit"])
        from tools.m222.offline_localization import score
        test = fold["test"]
        predictions = [learner.locate(frame, fit) for frame in test]
        center = [{"present": True, "center": CENTER} for _ in test]
        folds.append({"heldOut": fold["heldOut"], "fitRuns": fold["fitRuns"],
                      "fitOriginalFrames": len(fold["fit"]),
                      "heldOutOriginalFrames": len(test),
                      "duplicateSourceExcluded": fold["duplicateSourceExcluded"],
                      "trainingHumanAbsentCalibrationFrames": fit.train_absent_frames,
                      "frozenAbsentThreshold": fit.absent_threshold,
                      "imageModel": score(test, predictions),
                      "centerAlwaysPresentBaseline": score(test, center)})
    complete = learner.train([f for run in DEVELOPMENT for f in by_run[run]])
    model = model_pack(complete)
    model_digest = sha(canonical(model).encode())
    return {"schemaVersion": 1, "status": "EXPLORATORY_DEVELOPMENT_ONLY",
            "sourceArchiveSha256": SOURCE_ARCHIVE_SHA256,
            "sourceSeed": SOURCE_SEED, "numDistinctOriginalFrameDigests": distinct,
            "ownerReferenceCoverage": coverage,
            "evaluation": "whole-source-run-out with identical PNG SHA removed from fit; no prospective",
            "preregisteredImplementation": "tools/m223/multiscale_appearance.py",
            "folds": folds, "privateFinalFullDevelopmentModelSha256": model_digest,
            "privateFinalFullDevelopmentModel": model,
            "noGoByDefault": True,
            "warnings": ["NOT prospective: no O/P/Q source pixels accessed",
                         "No live agent integration or policy changes",
                         "Tiny, correlated source runs; y-location generalization not tested"]}


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--archive", type=Path, required=True)
    for run in DEVELOPMENT:
        p.add_argument("--labels-" + run[-1], type=Path, required=True)
    p.add_argument("--output", type=Path, required=True)
    args = p.parse_args()
    reject_unless(not args.output.exists(), "Refuse to overwrite a pre-existing private model.")
    study, coverage, distinct = validate_and_load(
        args.archive, [args.labels_l, args.labels_m, args.labels_n])
    print("M2.25 source and 3 owner cohorts VERIFIED; model is offline only.", flush=True)
    result = evaluate_with_locked_parameters(study, coverage, distinct)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    for fold in result["folds"]:
        print(fold["heldOut"], "visible", fold["imageModel"]["visibleFrames"],
              "located", fold["imageModel"]["localizedCorrect"],
              "falseAbsentPresence", fold["imageModel"]["falsePositive"],
              "threshold", round(fold["frozenAbsentThreshold"], 4), flush=True)
    print("PRIVATE model hash:", result["privateFinalFullDevelopmentModelSha256"],
          "Never publish owner labels or model weights.", flush=True)


if __name__ == "__main__":
    main()
