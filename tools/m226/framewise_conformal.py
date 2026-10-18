#!/usr/bin/env python3
"""M2.26 preregistered frame-wide conformal candidate calibration (offline only).

Corrects the M2.25 degenerate 1.0 sigmoid threshold without looking at ANY
prospective O/P/Q image. Private human labels never leave the local device.

CAVEAT: image-wise conformal false-positive bounds require exchangeability;
neighbouring game frames and phase shifts violate that assumption. Therefore
"p values" here are rank diagnostics, NOT validated probabilities of a player
nor a rigorous 20% error guarantee. Never use this model for live control.

This candidate was frozen *after* M2.25 L/M/N development outcomes were seen.
Those runs are REUSED DEVELOPMENT DIAGNOSTICS, NOT untouched validation.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
from tools.m223 import multiscale_appearance as base
from tools.m225.private_source_study import (validate_and_load, canonical, sha,
                                              SOURCE_ARCHIVE_SHA256, SOURCE_SEED)
from tools.m222.offline_localization import score

# Frozen before evaluating this candidate on source pixels.
ALPHA = 0.20
BACKGROUND_QUANTILE = 0.95
MIN_CALIBRATION_ABSENT_WHOLE_IMAGES = 4
MIN_FIT_VISIBLE = 3
MIN_FIT_ABSENT = 3
SUCCESS_MIN_VISIBLE_LOCATION_RECALL = 0.50
SUCCESS_MAX_ABSENT_FALSE_POSITIVE_RATE = 0.25
MODEL_NAME = "M2.26-framewise-contrastive-crossfit-v1"
RUNS = ("dev-l", "dev-m", "dev-n")
CENTER = (195, 422)


def demand(ok, explanation):
    if not ok:
        raise ValueError(explanation)


def split_fit_calibration(frames, train_run_ids):
    """Chronological even/odd HUMAN-ABSENT images per run; calibration excluded from fit.

    Deduplicate exact PNG SHA first; duplicated byte images cannot count twice.
    Avoid fitting or normalization on ANY calibration source image. Visible
    annotations are always fit-only; no true visible images in calibration.
    """
    demand(set(train_run_ids) <= set(RUNS) and len(train_run_ids) >= 2,
           "Only independent development fit runs allowed.")
    fit, calibration, globally_seen = [], [], set()
    for run in train_run_ids:
        samples = [f for f in frames[run] if f["state"] != "unknown"]
        deduped = []
        for frame in samples:
            digest = frame["sha"]
            if digest not in globally_seen:
                globally_seen.add(digest)
                deduped.append(frame)
        absent = [f for f in deduped if f["state"] == "absent"]
        # fixed chronological alternate split among true ABSENT frames.
        cal_sha = {f["sha"] for i, f in enumerate(absent) if i % 2 == 0}
        fit.extend(f for f in deduped if f["sha"] not in cal_sha)
        calibration.extend(f for f in absent if f["sha"] in cal_sha)
    fit_sha = {x["sha"] for x in fit}
    cal_sha = {x["sha"] for x in calibration}
    demand(not fit_sha.intersection(cal_sha), "Leakage: same original image in fit and calibration.")
    demand(len(cal_sha) == len(calibration) >= MIN_CALIBRATION_ABSENT_WHOLE_IMAGES,
           "NOT_READY: no independent human-ABSENT calibration images.")
    demand(sum(f["state"] == "visible" for f in fit) >= MIN_FIT_VISIBLE and
           sum(f["state"] == "absent" for f in fit) >= MIN_FIT_ABSENT,
           "NOT_READY: fitting needs genuine VISIBLE and ABSENT whole screenshots.")
    return fit, calibration


def image_evidence(frame, model, grid=None):
    """One image-wide statistic: highest *unclipped logit* minus image 95th percentile.

    No final sigmoid, avoiding probability=1 numeric saturation; scan the
    entire image, so multiple candidate comparisons occur INSIDE the same
    frame and are calibrated using held-out WHOLE frames.
    """
    if grid is None:
        grid = base.GRID
    demand(len(grid) >= 2, "Need nonempty full-image candidate search.")
    matrix = base.patches(frame, grid)
    raw = ((matrix - model.mean) / model.std) @ model.weights + model.bias
    demand(bool(np.isfinite(raw).all()), "Unusable numeric candidate evidence.")
    winner = int(np.argmax(raw))
    evidence = float(raw[winner] - np.quantile(raw, BACKGROUND_QUANTILE))
    demand(math.isfinite(evidence) and evidence >= -1e-8,
           "Corrupt image-wide candidate-contrast score.")
    return evidence, tuple(grid[winner])


def rank_tail_probability(image_score, absent_calibration_scores):
    """Conservative strict greater/equal treatment for tied absent images.

    May return p=1 when all frames have equal contrast (correct abstention).
    Finite-sample resolution 1/(n_cal+1), never an imaginary 0 or 1.0
    saturated sigmoid gate.
    """
    samples = np.asarray(absent_calibration_scores, dtype=float)
    demand(samples.ndim == 1 and len(samples) >= MIN_CALIBRATION_ABSENT_WHOLE_IMAGES and
           np.isfinite(samples).all() and math.isfinite(image_score),
           "NOT_READY: human-ABSENT whole-frame calibration unavailable.")
    return float((1 + np.count_nonzero(samples >= image_score)) / (1 + len(samples)))


def predict(frame, model, absent_scores, grid=None):
    raw, position = image_evidence(frame, model, grid)
    p = rank_tail_probability(raw, absent_scores)
    accepted = p <= ALPHA
    return {"present": accepted, "center": position if accepted else None,
            "framewideContrast": round(raw, 6), "calibrationRankP": round(p, 6),
            "numIndependentAbsentCalibrationFrames": len(absent_scores)}


def fit_with_calibration(data, train_runs, *, grid=None):
    fit, calibration = split_fit_calibration(data, train_runs)
    if grid is None:
        grid = base.GRID
    # Frozen M2.23 patch descriptor and coefficients, but deliberately
    # do not use the original sigmoid probability threshold.
    fit_model = base.train(fit, grid=grid)
    cal_scores = [image_evidence(frame, fit_model, grid)[0] for frame in calibration]
    demand(len(cal_scores) >= MIN_CALIBRATION_ABSENT_WHOLE_IMAGES,
           "NOT_READY: not enough out-of-fit WHOLE-ABSENT score controls.")
    return fit_model, cal_scores, fit, calibration


def model_serialize(model, cal_scores, fit_sha, cal_sha):
    return {"schemaVersion": 1, "method": MODEL_NAME, "alpha": ALPHA,
            "backgroundQuantile": BACKGROUND_QUANTILE,
            "candidateSpacingPixels": base.STRIDE,
            "featureDimension": int(model.weights.size),
            "mean": model.mean.tolist(), "std": model.std.tolist(),
            "weights": model.weights.tolist(), "bias": float(model.bias),
            "trainingOriginalFrameSha256": sorted(fit_sha),
            "calibrationOriginalAbsentFrameSha256": sorted(cal_sha),
            "calibrationWholeFrameContrast": [float(v) for v in cal_scores],
            "calibrationSize": len(cal_scores)}


def evaluate(by_run, coverage, distinct):
    demand(set(by_run) == set(RUNS),
           "Only three original owner-admitted development runs may be evaluated.")
    folds = []
    for held in RUNS:
        hidden = {f["sha"] for f in by_run[held]}
        training = {run: [f for f in by_run[run] if f["sha"] not in hidden]
                    for run in RUNS if run != held}
        model, cal_scores, fit, cal = fit_with_calibration(training, list(training))
        test = [f for f in by_run[held] if f["state"] != "unknown"]
        predictions = [predict(f, model, cal_scores) for f in test]
        center = [{"present": True, "center": CENTER} for _ in test]
        image_metrics = score(test, predictions)
        baseline = score(test, center)
        absent_frames = image_metrics["falsePositive"] + image_metrics["trueNegative"]
        go_fold = (image_metrics["localizationRecall"] >= SUCCESS_MIN_VISIBLE_LOCATION_RECALL and
                   image_metrics["falsePositive"] / max(1, absent_frames) <= SUCCESS_MAX_ABSENT_FALSE_POSITIVE_RATE)
        folds.append({"heldOut": held,
                      "fittedOnRuns": sorted(training),
                      "excludedSharedOriginalPngs": sum(f["sha"] in hidden for frames in training.values() for f in frames),
                      "fitOriginalFrames": len(fit),
                      "calibrationIndependentWholeAbsentFrames": len(cal),
                      "calibrationMinimumAttainableP": round(1 / (len(cal)+1), 6),
                      "metrics": image_metrics, "centerBaseline": baseline,
                      "passesPreregisteredPerRunGate": go_fold})
    # Final development model intended ONLY for private reproducibility.
    model, absent_scores, fit, cal = fit_with_calibration(by_run, list(RUNS))
    packed = model_serialize(model, absent_scores,
                             {f["sha"] for f in fit}, {f["sha"] for f in cal})
    signature = sha(canonical(packed).encode())
    summary = {"schemaVersion": 1, "status": "EXPLORATORY_DEVELOPMENT_ONLY",
               "candidate": MODEL_NAME,
               "ownerCoverage": coverage, "sourceStudySeed": SOURCE_SEED,
               "sourceArchiveSha256": SOURCE_ARCHIVE_SHA256,
               "uniqueOriginalImageSha256": distinct,
               "precommittedAlpha": ALPHA,
               "precommittedBackgroundQuantile": BACKGROUND_QUANTILE,
               "goGate": {"minVisibleLocalizationRecallPerWholeRun": SUCCESS_MIN_VISIBLE_LOCATION_RECALL,
                          "maxAbsentFalsePositiveRatePerWholeRun": SUCCESS_MAX_ABSENT_FALSE_POSITIVE_RATE},
               "folds": folds,
               "allDevelopmentFoldsPass": all(x["passesPreregisteredPerRunGate"] for x in folds),
               "privateDevelopmentModelHash": signature,
               "privateDevelopmentModel": packed,
               "prospectiveUsed": False, "livePolicyChanged": False,
               "warning": "Post-M2.25 development diagnostics only, not unseen or exchangeable calibration evidence."}
    return summary


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--archive", required=True, type=Path)
    for run in RUNS:
        p.add_argument("--labels-" + run[-1], type=Path, required=True)
    p.add_argument("--output", required=True, type=Path)
    args = p.parse_args()
    demand(not args.output.exists(), "Refuse to overwrite original private report.")
    data, coverage, unique = validate_and_load(
        args.archive, [args.labels_l, args.labels_m, args.labels_n])
    print("PASS original-owner source SHA, labels, all per-run gates; prospective untouched.", flush=True)
    result = evaluate(data, coverage, unique)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n")
    for row in result["folds"]:
        print(row["heldOut"], "correct", row["metrics"]["localizedCorrect"],
              "/", row["metrics"]["visibleFrames"], "absentFP", row["metrics"]["falsePositive"],
              "cal n", row["calibrationIndependentWholeAbsentFrames"], flush=True)
    print("M2.26 private development model SHA", result["privateDevelopmentModelHash"],
          "all-fold-go:", result["allDevelopmentFoldsPass"],
          "NOT independent prospective evidence", flush=True)


if __name__ == "__main__":
    main()
