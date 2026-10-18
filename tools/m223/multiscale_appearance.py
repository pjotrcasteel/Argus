#!/usr/bin/env python3
"""ARGUS M2.23: preregistered stronger image-only research representation.

Not used by live ARGUS. No embedded player palette, game source, owner truth,
weights, precomputed real-game samples or tuned I/J/K results. Fold-dependent
calibration uses TRAINING-ONLY human-ABSENT whole screenshots, not patches
pretending to be independent absences.

The source input will be independently frozen before prospective evaluation.
"""
from dataclasses import dataclass
import math
import numpy as np

SIZE = 64
BINS = 8
CELLS = 4
STRIDE = 20
GRID = tuple((x, y) for y in range(12, 844, STRIDE)
             for x in range(10, 390, STRIDE))
NEGATIVE_GRID = GRID[::9]
EXCLUSION_PIXELS = 70
LOCATION_TOLERANCE = 35
EPOCHS = 1000
LEARNING_RATE = 0.08
L2 = 0.08
ABSENT_MARGIN = .025
MAX_TRAIN_FRAMES = 300
# Locked prior to any real M2.23 human labels or prospective evaluation.


def require(ok, reason):
    if not ok:
        raise ValueError(reason)


def descriptor(rgb, x, y):
    """246-dimensional patch appearance; no coordinate is returned as a feature.

    Uses 4x4 RGB color means and deviations, spatial HOG orientation/energy
    and global RGB statistics, never x/y or external player palette.
    """
    require(isinstance(rgb, np.ndarray) and rgb.shape == (844, 390, 3)
            and rgb.dtype == np.uint8, "Expected raw 390x844 RGB pixels.")
    require(type(x) is int and type(y) is int and 0 <= x < 390 and 0 <= y < 844,
            "Invalid image pixel sampling center.")
    patch = np.pad(rgb, ((32, 32), (32, 32), (0, 0)), mode="edge")[
        y:y + SIZE, x:x + SIZE].astype(np.float32) / 255.
    require(patch.shape == (64, 64, 3), "Patch boundaries invalid.")
    lum = patch[..., 0] * .2126 + patch[..., 1] * .7152 + patch[..., 2] * .0722
    gy, gx = np.gradient(lum)
    orientation = (np.arctan2(gy, gx) + np.pi) * (BINS / (2 * np.pi))
    bins = np.floor(orientation).astype(np.int32) % BINS
    magnitude = np.sqrt(gx * gx + gy * gy)
    features = [np.mean(patch, axis=(0, 1)), np.std(patch, axis=(0, 1))]
    hog = []
    for cy in range(CELLS):
        for cx in range(CELLS):
            ys = slice(cy * 16, (cy + 1) * 16)
            xs = slice(cx * 16, (cx + 1) * 16)
            colors = patch[ys, xs, :]
            features.extend((colors.mean(axis=(0, 1)), colors.std(axis=(0, 1))))
            hist = np.bincount(bins[ys, xs].ravel(),
                               weights=magnitude[ys, xs].ravel(), minlength=BINS)
            hog.extend(hist.tolist())
            features.append(np.asarray([float(magnitude[ys, xs].mean())]))
    hog_array = np.asarray(hog, dtype=np.float64)
    # Normalize edge orientation energy so size-independent patterns can compete.
    # Keep raw cell magnitude separately (in other 16 components).
    hog_array /= np.sqrt(np.sum(hog_array * hog_array) + 1e-8)
    features.append(hog_array)
    vec = np.concatenate(features).astype(np.float64)
    require(vec.shape == (246,) and bool(np.isfinite(vec).all()),
            "Malformed multiscale RGB/HOG descriptor.")
    return vec


@dataclass(frozen=True)
class OfflineModel:
    mean: np.ndarray
    std: np.ndarray
    weights: np.ndarray
    bias: float
    absent_threshold: float
    train_positive_patches: int
    train_negative_patches: int
    train_absent_frames: int

    def probability(self, matrix):
        z = np.clip(((matrix - self.mean) / self.std) @ self.weights + self.bias, -35, 35)
        return 1 / (1 + np.exp(-z))


def patches(frame, grid=GRID):
    image = frame["rgb"]
    return np.stack([descriptor(image, x, y) for x, y in grid])


def train(frames, *, grid=GRID, negative_grid=NEGATIVE_GRID):
    require(0 < len(frames) <= MAX_TRAIN_FRAMES, "Expected bounded independent source frames.")
    rows, labels, absent = [], [], []
    seen = set()
    for f in frames:
        require(f["state"] in ("visible", "absent", "unknown"), "Invalid original owner state.")
        require(f["sha"] and len(f["sha"]) == 64, "Immutable source SHA required.")
        # Exact duplicate screenshot within fit group is not counted repeatedly.
        if f["sha"] in seen:
            continue
        seen.add(f["sha"])
        if f["state"] == "unknown":
            continue
        p = f.get("center")
        require((p is None) == (f["state"] == "absent"), "Contradictory source reference.")
        if p is not None:
            rows.append(descriptor(f["rgb"], int(p["x"]), int(p["y"])))
            labels.append(1)
        else:
            absent.append(f)
        for x, y in negative_grid:
            if p is not None and ((x - p["x"]) ** 2 + (y - p["y"]) ** 2
                                   < EXCLUSION_PIXELS ** 2):
                continue
            rows.append(descriptor(f["rgb"], x, y))
            labels.append(0)
    require(len(absent) >= 3, "NOT_READY: calibration requires >=3 whole ABSENT training images.")
    y = np.asarray(labels, dtype=np.float64)
    pos, neg = int(np.sum(y == 1)), int(np.sum(y == 0))
    require(pos >= 3 and neg >= 3, "NOT_READY: need >=3 true positive and negative image patches.")
    x = np.stack(rows)
    mean = x.mean(axis=0)
    std = np.maximum(x.std(axis=0), 1e-5)
    z = (x - mean) / std
    weights = np.zeros(x.shape[1], dtype=np.float64)
    bias = 0.
    balance = np.where(y == 1, .5 / pos, .5 / neg)
    for _ in range(EPOCHS):
        logits = np.clip(z @ weights + bias, -35, 35)
        error = (1 / (1 + np.exp(-logits)) - y) * balance
        weights -= LEARNING_RATE * (z.T @ error + L2 * weights)
        bias -= LEARNING_RATE * float(error.sum())
    initial = OfflineModel(mean, std, weights, float(bias), 0.5, pos, neg, len(absent))
    # Calibrate only using human-confirmed whole ABSENT training images.
    # A pool of absent patches is NOT a substitute for image-level absence.
    maxima = [float(initial.probability(patches(f, grid)).max()) for f in absent]
    threshold = min(1., max(.5, max(maxima) + ABSENT_MARGIN))
    return OfflineModel(mean, std, weights, float(bias), threshold, pos, neg, len(absent))


def locate(frame, model, *, grid=GRID):
    probs = model.probability(patches(frame, grid))
    i = int(np.argmax(probs))
    best = float(probs[i])
    accepted = bool(best > model.absent_threshold)
    return {"present": accepted, "center": tuple(grid[i]) if accepted else None,
            "confidence": round(best, 6),
            "threshold": round(model.absent_threshold, 6)}


def independent_folds(by_run):
    """Deterministic whole-run-out and same-byte exclusion; no private heldout."""
    runs = sorted(by_run)
    require(len(runs) >= 3 and all(r.startswith("dev-") and r != "run-01" for r in runs),
            "NOT_READY: >=3 independent development runs required; held-out run-01 forbidden.")
    for held in runs:
        test = [f for f in by_run[held] if f["state"] != "unknown"]
        forbidden = {f["sha"] for f in by_run[held]}
        train_frames = [f for run in runs if run != held for f in by_run[run]
                        if f["sha"] not in forbidden]
        require(len(test) > 0, "NOT_READY: held-out source run needs human labels.")
        yield {"heldOut": held, "fitRuns": [run for run in runs if run != held],
               "fit": train_frames, "test": test,
               "duplicateSourceExcluded": sum(
                   f["sha"] in forbidden for run in runs if run != held for f in by_run[run])}


def evaluate(by_run):
    """Score only after freeze. This method must NOT execute in public CI on real data."""
    from tools.m222.offline_localization import score
    folds = []
    for fold in independent_folds(by_run):
        model = train(fold["fit"])
        predictions = [locate(f, model) for f in fold["test"]]
        baseline = [{"present": True, "center": (195, 422)} for _ in fold["test"]]
        folds.append({"heldOut": fold["heldOut"], "fitRuns": fold["fitRuns"],
                      "duplicateSourceExcluded": fold["duplicateSourceExcluded"],
                      "absentCalibrationImages": model.train_absent_frames,
                      "absentThreshold": round(model.absent_threshold, 6),
                      "model": score(fold["test"], predictions),
                      "centerBaseline": score(fold["test"], baseline)})
    return {"status": "EXPLORATORY_DEVELOPMENT_ONLY", "folds": folds,
            "warning": "NOT a prospective test or live policy; no run-01 accessed."}
