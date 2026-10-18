#!/usr/bin/env python3
"""M2.22 exploratory screenshot-only spatial learner.

Local-only private input. The source archive is authenticated using the exact
M2.21 frozen hashes, and private human references with the M2.20 gate.
Folds exclude one ENTIRE development run and exact duplicate PNG bytes from
remaining training runs. Human locations never enter model features.
No use of game internals, outcomes, online inference, held-out run-01 or policy.
"""
import argparse
import hashlib
import json
import math
import sys
import tempfile
from collections import Counter
from io import BytesIO
from pathlib import Path
from zipfile import ZipFile

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
from tools.m220.point_cohort import RUNS, SEED, validate_owner
from tools.m221.publish_consented_public import members

FEATURE_NAMES = ["mean_luminance", "luminance_std", "ring_contrast",
                 "gradient_density", "mean_saturation"]
GRID = tuple((x, y) for y in sorted(set(range(12, 844, 24)) | {831})
             for x in sorted(set(range(10, 390, 24)) | {379}))
NEGATIVE_GRID = GRID[::10]
LOCATION_TOLERANCE = 35
EXCLUSION_RADIUS = 70
PRESENCE_THRESHOLD = 0.50


def require(ok, message):
    if not ok:
        raise ValueError(message)


def read_source_and_private(archive: Path, labels: list[Path]):
    # The validated public-only bundle deliberately excludes original trace
    # JSONL and private labels; names, ZIP SHA and all 54 image digests are pinned.
    images, _ = members(archive)
    with tempfile.TemporaryDirectory(prefix="argus-m222-private-") as scratch:
        site = Path(scratch)
        with ZipFile(archive) as zipped:
            plan = zipped.read("m220-study-plan.json")
        (site / "m220-study-plan.json").write_bytes(plan)
        for name, raw in images.items():
            path = site / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
        admission = validate_owner(site, labels)
    require(admission["status"] == "STRUCTURALLY_ELIGIBLE",
            "NOT_READY: private human class/off-center reference coverage insufficient.")
    owner = {json.loads(p.read_text())["runId"]: json.loads(p.read_text()) for p in labels}
    require(set(owner) == set(RUNS), "Missing independent private owner run.")
    data = {}
    for run in RUNS:
        arr = []
        for frame in owner[run]["frames"]:
            step = frame["step"]
            name = f"annoteren/{run}/frame-{step:04d}.png"
            require(name in images, "Frozen original image missing.")
            raw = images[name]
            require(hashlib.sha256(raw).hexdigest() == frame["screenshotSha256"],
                    "Changed owner screenshot hash.")
            with Image.open(BytesIO(raw)) as original:
                pixels = np.asarray(original.convert("RGB"), dtype=np.uint8)
            require(pixels.shape == (844, 390, 3),
                    "Original PNG does not decode as an authentic 390x844 screenshot.")
            arr.append({"run": run, "step": step, "sha": frame["screenshotSha256"],
                        "state": frame["player"]["state"],
                        "center": frame["player"]["center"], "rgb": pixels})
        require(len(arr) == 18, "Frozen development run frame count changed.")
        data[run] = arr
    return data, admission["runs"]


def features(padded, x, y):
    # Edge padding is explicit; x/y are ONLY used to index image RGB.
    x += 36
    y += 36
    rgb = padded[y - 26:y + 27, x - 26:x + 27].astype(np.float32) * (1 / 255)
    big = padded[y - 34:y + 35, x - 34:x + 35].astype(np.float32) * (1 / 255)
    lum = rgb[..., 0] * .2126 + rgb[..., 1] * .7152 + rgb[..., 2] * .0722
    around = big[..., 0] * .2126 + big[..., 1] * .7152 + big[..., 2] * .0722
    ring = (float(around.sum()) - float(lum.sum())) / (69 * 69 - 53 * 53)
    dx = np.abs(np.diff(lum, axis=1))
    dy = np.abs(np.diff(lum, axis=0))
    return np.array([float(lum.mean()), float(lum.std()), float(lum.mean()) - ring,
                     (float((dx > .16).mean()) + float((dy > .16).mean())) / 2,
                     float((rgb.max(axis=2) - rgb.min(axis=2)).mean())], dtype=np.float64)


def measure_all(data):
    for frames in data.values():
        for frame in frames:
            padded = np.pad(frame.pop("rgb"),
                            ((36, 36), (36, 36), (0, 0)), mode="edge")
            frame["scan"] = np.stack([features(padded, x, y) for x, y in GRID])
            frame["negatives"] = np.stack([features(padded, x, y) for x, y in NEGATIVE_GRID])
            c = frame["center"]
            frame["positive"] = features(padded, c["x"], c["y"]) if c else None


def fit(train):
    rows = []
    labels = []
    for frame in train:
        if frame["state"] == "unknown":
            continue
        if frame["state"] == "visible":
            rows.append(frame["positive"])
            labels.append(1)
        for (x, y), image_features in zip(NEGATIVE_GRID, frame["negatives"]):
            c = frame["center"]
            if c is not None and (x - c["x"]) ** 2 + (y - c["y"]) ** 2 < EXCLUSION_RADIUS ** 2:
                continue
            rows.append(image_features)
            labels.append(0)
    require(bool(rows), "No private source patches to fit.")
    x = np.stack(rows)
    y = np.asarray(labels, dtype=np.float64)
    positives, negatives = int(sum(y == 1)), int(sum(y == 0))
    require(positives >= 3 and negatives >= 3, "Whole-run training lacks both classes.")
    mean = x.mean(axis=0)
    std = np.maximum(x.std(axis=0), 1e-5)
    x = (x - mean) / std
    weight = np.zeros(x.shape[1], dtype=np.float64)
    bias = 0.
    balanced = np.where(y == 1, .5 / positives, .5 / negatives)
    # Frozen optimizer, penalty, learning rate and step count; never calibrated
    # to the private test fold's human labels.
    for _ in range(1500):
        z = np.clip(x @ weight + bias, -35, 35)
        error = (1 / (1 + np.exp(-z)) - y) * balanced
        weight -= .12 * (x.T @ error + .04 * weight)
        bias -= .12 * float(error.sum())
    return mean, std, weight, bias, positives, negatives


def classify(frame, model):
    mean, std, weight, bias, *_ = model
    logits = np.clip(((frame["scan"] - mean) / std) @ weight + bias, -35, 35)
    scores = 1 / (1 + np.exp(-logits))
    index = int(np.argmax(scores))
    probability = float(scores[index])
    return {"present": probability >= PRESENCE_THRESHOLD,
            "center": GRID[index] if probability >= PRESENCE_THRESHOLD else None,
            "confidence": round(probability, 5)}


def score(truths, predictions):
    out = {"truePositive": 0, "falsePositive": 0, "falseNegative": 0, "trueNegative": 0,
           "localizedCorrect": 0, "visibleFrames": 0}
    require(len(truths) == len(predictions), "Mismatch in evaluation count.")
    for truth, pred in zip(truths, predictions):
        require(truth["state"] in ("visible", "absent"),
                "Do not convert UNKNOWN human frames to negatives.")
        visible = truth["state"] == "visible"
        candidate = bool(pred["present"])
        if visible and candidate: out["truePositive"] += 1
        elif visible: out["falseNegative"] += 1
        elif candidate: out["falsePositive"] += 1
        else: out["trueNegative"] += 1
        if visible:
            out["visibleFrames"] += 1
            if candidate:
                x, y = pred["center"]
                c = truth["center"]
                out["localizedCorrect"] += int((x - c["x"]) ** 2 + (y - c["y"]) ** 2
                                               <= LOCATION_TOLERANCE ** 2)
    tp, fp, fn = out["truePositive"], out["falsePositive"], out["falseNegative"]
    out["presencePrecision"] = round(tp / max(1, tp + fp), 4)
    out["presenceRecall"] = round(tp / max(1, tp + fn), 4)
    out["localizationRecall"] = round(out["localizedCorrect"] / max(1, out["visibleFrames"]), 4)
    return out


def evaluate(data, coverage):
    require(set(data) == set(RUNS), "Only frozen development run identities are accepted.")
    measure_all(data)
    folds = []
    for held in RUNS:
        test = [f for f in data[held] if f["state"] != "unknown"]
        excluded_sha = {frame["sha"] for frame in data[held]}
        candidates = [f for name in RUNS if name != held for f in data[name]]
        train = [f for f in candidates if f["sha"] not in excluded_sha]
        model = fit(train)
        predictions = [classify(f, model) for f in test]
        center_baseline = [{"present": True, "center": (195, 422)} for _ in test]
        folds.append({"heldOutRun": held, "fitRuns": [r for r in RUNS if r != held],
                      "fitImages": len(train),
                      "sharedOriginalPNGExcluded": len(candidates) - len(train),
                      "fitPositivePatches": model[4], "fitNegativePatches": model[5],
                      "imageModel": score(test, predictions),
                      "centerAlwaysPresentBaseline": score(test, center_baseline)})
    final = fit([f for run in RUNS for f in data[run]])
    return {"schemaVersion": 1, "studySeed": SEED, "status": "EXPLORATORY_OFFLINE_TRAINED",
            "coverage": coverage, "protocol": {
                "imageOnlyFeatures": FEATURE_NAMES, "scanPositions": len(GRID),
                "candidateGridStepPixels": 24,
                "presenceThreshold": PRESENCE_THRESHOLD,
                "locationTolerancePixels": LOCATION_TOLERANCE,
                "normalization": "fit-run-only",
                "validation": "leave-one-entire-development-run-out, exact PNG SHA excluded"},
            "folds": folds,
            "privateResearchModel": {"featureNames": FEATURE_NAMES, "trainingRuns": list(RUNS),
                                     "means": final[0].tolist(), "std": final[1].tolist(),
                                     "weights": final[2].tolist(), "bias": final[3]},
            "warning": "Exploratory, development only. Not a validated general player detector. "
                       "Repeated screenshots are correlated; no external prospective tests, "
                       "no game policy integration."}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--archive", type=Path, required=True)
    for run in RUNS:
        parser.add_argument("--labels-" + run[-1], type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    data, coverage = read_source_and_private(args.archive, [args.labels_i, args.labels_j, args.labels_k])
    report = evaluate(data, coverage)
    require(not args.output.exists(), "Never silently overwrite an existing private research result.")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("M2.22 PRIVATE:", report["status"])
    for fold in report["folds"]:
        print(fold["heldOutRun"], "localization:", fold["imageModel"]["localizedCorrect"],
              "of", fold["imageModel"]["visibleFrames"], "center-baseline:",
              fold["centerAlwaysPresentBaseline"]["localizedCorrect"],
              "shared-excluded:", fold["sharedOriginalPNGExcluded"])
    print("Private input and weights must never be uploaded to GitHub Actions/Pages.")


if __name__ == "__main__":
    main()
