#!/usr/bin/env python3
"""M2.36: train a game-NEUTRAL appearance metric from authentic PRIVATE references.

Research only. Hard negative crops from labeled training screens + genuinely
ABSENT whole screenshots teach a distance metric, not a hard-coded mage color.
No user labels are read at inference. The separate causal M2.35 tracker can
link candidates, but this module cannot assert 'verified player identity'.
"""
import hashlib
import json
import math

import numpy as np

from tools.m230 import identity_study as appearance
from tools.m235.causal_identity import Candidate, SUPPRESSION_PX, MAX_CANDIDATES

MODEL_ID = "m236-player-exemplar-contrastive-diagonal-metric-v1"
MIN_FIT_VISIBLE = 8
MIN_FIT_ABSENT = 2
HARD_NEGATIVE_FRACTION = .25
CONTRAST_RIDGE = .02
MAX_DIAGONAL_WEIGHT = 10.
MEAN_DISTANCE_FRACTION = .30
MIN_ABSENT_CONTROL = 4
MIN_ABSENT_SOURCE_RUNS = 2
ABSENT_MARGIN = .02
MIN_TWO_OBJECT_LOGIT_MARGIN = .04


def require(ok, description):
    if not ok:
        raise ValueError(description)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def fit_metric(source):
    """Human truth exclusively in fitting runs; no held-out source ever enters.

    Fit rows must already be admitted and whole-run/SHA separated by caller.
    A positive player patch comes ONLY from a labeled training image.
    63 diversified background candidates and real ABSENT whole images
    contribute negatives. Pixel x/y is used only to EXCLUDE potential
    player at training time; x/y cannot enter model feature dimensions.
    """
    samples = list(source)
    require(len({r["sha"] for r in samples}) == len(samples) and
            all(r["state"] in ("visible", "absent") and r.get("center") is None
                if r["state"] == "absent" else
                r["state"] == "visible" and r.get("center") is not None
                for r in samples),
            "Fit source SHA must be unique with verified VISIBLE/ABSENT observations.")
    require(sum(r["state"] == "visible" for r in samples) >= MIN_FIT_VISIBLE and
            sum(r["state"] == "absent" for r in samples) >= MIN_FIT_ABSENT,
            "NOT_READY: requires real fitting player positives and ABSENT images.")
    positives, negatives, known_runs = [], [], set()
    for row in samples:
        require(row["rgb"].shape == (844, 390, 3) and row["rgb"].dtype == np.uint8 and
                type(row["run"]) is str and row["run"] and
                type(row["sha"]) is str and len(row["sha"]) == 64,
                "Original fit reference screenshot invalid.")
        known_runs.add(row["run"])
        center = row["center"]
        if center is not None:
            require(type(center["x"]) is int and type(center["y"]) is int and
                    0 <= center["x"] < 390 and 0 <= center["y"] < 844,
                    "Only original-pixel authenticated fit center allowed.")
            positives.append(appearance.descriptor(
                appearance.padded(row["rgb"]), center["x"], center["y"]))
        points = [(x,y) for x,y in appearance.NEGATIVE_GRID
                  if center is None or
                  (x-center["x"])**2+(y-center["y"])**2 >= appearance.EXCLUDE_RADIUS**2]
        negatives.extend(appearance.features(row["rgb"], points))
    require(len(known_runs) >= 2, "NOT_READY: fitting requires >=2 development source runs.")
    p = np.asarray(positives, np.float64)
    n = np.asarray(negatives, np.float64)
    require(len(n) > 10 and np.isfinite(p).all() and np.isfinite(n).all(),
            "Insufficient genuine negative training reference crops.")
    mean = p.mean(axis=0)
    # Fit hard-negative emphasis only from original fitting images, never
    # from held-run or calibration screenshots.
    distances = np.mean((n-mean)**2, axis=1)
    count = max(1,int(math.ceil(len(n)*HARD_NEGATIVE_FRACTION)))
    hard = n[np.argsort(distances,kind="stable")[:count]]
    positive_variance = np.mean((p-mean)**2,axis=0)
    contrast_difference = np.mean((hard-mean)**2,axis=0)
    gain = np.clip((contrast_difference-positive_variance)/
                   (positive_variance+CONTRAST_RIDGE),0,MAX_DIAGONAL_WEIGHT)
    require(np.isfinite(gain).all(), "Nonfinite contrastive player metric.")
    # Never let one feature dominate; a uniform prior handles tiny data.
    weights = 1.+gain
    weights /= float(weights.mean())
    return {"modelId":MODEL_ID, "centroid":mean, "prototypes":p,
            "weights":weights, "fitRuns":tuple(sorted(known_runs)),
            "fitPositiveScreenshots":len(p), "fitHardNegativePatches":len(hard),
            "fitImageHashes":tuple(sorted(r["sha"] for r in samples))}


def score_features(vectors, model):
    x = np.asarray(vectors,np.float64)
    p = np.asarray(model["prototypes"],np.float64)
    mu = np.asarray(model["centroid"],np.float64)
    w = np.asarray(model["weights"],np.float64)
    require(x.ndim==2 and p.ndim==2 and x.shape[1]==p.shape[1]==mu.size==w.size and
            np.isfinite(x).all() and np.isfinite(p).all() and
            np.isfinite(mu).all() and np.isfinite(w).all() and (w>0).all(),
            "Learned source-only appearance descriptor dimensions invalid.")
    norm = float(w.sum())
    dist = (np.sum(x*x*w,axis=1)[:,None] +
            np.sum(p*p*w,axis=1)[None,:] -
            2*(x*w)@p.T)/norm
    nearest = np.maximum(np.min(dist,axis=1),0)
    avg = np.sum((x-mu)**2*w,axis=1)/norm
    return -((1-MEAN_DISTANCE_FRACTION)*nearest+MEAN_DISTANCE_FRACTION*avg)


def source_candidates(previous_rgb, current_rgb, model, *,
                      grid=appearance.GRID, limit=MAX_CANDIDATES):
    """No player labels or privileged target info. Rank full original image."""
    require(1<=limit<=MAX_CANDIDATES and len(grid)>=2,
            "Bounded full-image independent source candidates required.")
    prev, current = appearance.padded(previous_rgb),appearance.padded(current_rgb)
    descriptors = appearance.features(current_rgb,grid)
    logits=score_features(descriptors,model)
    require(bool(np.isfinite(logits).all()),"Nonfinite private learned candidate logit.")
    order = sorted(range(len(grid)),key=lambda i:(-logits[i],grid[i][1],grid[i][0]))
    proposals=[]
    for i in order:
        x,y=grid[i]
        if any((x-p.x)**2+(y-p.y)**2<SUPPRESSION_PX**2 for p in proposals):
            continue
        diff=float(np.abs(current[y:y+48,x:x+48].astype(np.float32) -
                          prev[y:y+48,x:x+48].astype(np.float32)).mean()/255.)
        # Weighted shape is representation-only, not proof of player identity.
        weighted=np.sqrt(model["weights"])*descriptors[i]
        proposals.append(Candidate(int(x),int(y),tuple(float(v) for v in weighted),
                                   float(logits[i]),diff))
        if len(proposals)>=limit:
            break
    return proposals


def calibrate_absent(original_absent_sources, model, *, grid=appearance.GRID):
    """Conservative whole ABSENT screen max, on completely disjoint images."""
    control = list(original_absent_sources)
    require(len(control)>=MIN_ABSENT_CONTROL and
            len({r["sha"] for r in control})==len(control) and
            len({r["run"] for r in control})>=MIN_ABSENT_SOURCE_RUNS and
            all(r["state"]=="absent" and r["center"] is None for r in control) and
            not ({r["sha"] for r in control}&set(model["fitImageHashes"])),
            "NOT_READY: 4 distinct whole-ABSENT calibration originals from >=2 other runs.")
    best=[]
    for row in control:
        points=appearance.features(row["rgb"],grid)
        score=score_features(points,model)
        best.append(float(score.max()))
    return max(best)+ABSENT_MARGIN


def classify_shadow(proposals, threshold):
    """Not a live player detector: hypotheses can still be monsters or HUD."""
    require(math.isfinite(threshold) and type(proposals) in (list,tuple) and
            all(isinstance(c,Candidate) for c in proposals),
            "Candidate / frozen genuine ABSENT calibration invalid.")
    ordered=sorted(proposals,key=lambda c:(-c.image_logit,c.y,c.x))
    if not ordered or ordered[0].image_logit<=threshold:
        return {"status":"SOURCE_ONLY_ABSTAIN","candidate":None,
                "playerPresent":None,"verifiedPlayerCenter":None}
    if len(ordered)>1 and ordered[0].image_logit-ordered[1].image_logit < MIN_TWO_OBJECT_LOGIT_MARGIN:
        return {"status":"LOOKALIKE_AMBIGUITY_ABSTAIN","candidate":None,
                "playerPresent":None,"verifiedPlayerCenter":None}
    return {"status":"UNVERIFIED_APPEARANCE_CANDIDATE",
            "candidate":{"x":ordered[0].x,"y":ordered[0].y},
            "playerPresent":None,"verifiedPlayerCenter":None}


def model_digest(model, threshold):
    values={k: (v.tolist() if isinstance(v,np.ndarray) else v)
            for k,v in model.items()}
    values["frozenThreshold"]=threshold
    return sha(json.dumps(values,sort_keys=True,separators=(",",":")).encode())
