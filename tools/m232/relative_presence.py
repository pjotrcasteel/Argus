#!/usr/bin/env python3
"""M2.32 frozen *hypothesis* for future genuine new U/V/W development labels.

Image appearance score comes from local learned shape (M2.30), but presence
evidence must be image-relative and spatially unique rather than uncalibrated
max absolute logit. NO pixel from sealed O/P/Q or owner labels here.
This is not player identity and is NOT used by the live actor.
"""
import math
import numpy as np

MIN_SEPARATION_PIXELS = 64
BACKGROUND_QUANTILE = .90
BACKGROUND_QUARTILE_LOW = .25
BACKGROUND_QUARTILE_HIGH = .75
RAW_SCORE_SPREAD_FLOOR = .25
ABSENT_SCORE_MARGIN = .02
MIN_INDEPENDENT_ABSENT = 4
MIN_ABSENT_RUNS = 2
MIN_TRUE_VISIBLE_PER_HELD_RUN = 8
MIN_TRUE_ABSENT_PER_HELD_RUN = 3
MIN_VISIBLE_CENTER_RECALL = .50
MAX_ABSENT_FALSE_PRESENCE = .25
CENTER_TOLERANCE = 35
MODEL_HYPOTHESIS = "M2.32-independent-identity-spatial-contrast-and-whole-absence-v1"


def require(ok, why):
    if not ok:
        raise ValueError(why)


def image_evidence(logits, centers):
    """Non-oracular top score *both above background* and *above competing objects*.

    No absolute position enters the patch learner; spatial separation here
    merely identifies a distinct runner-up object, not character identity.
    Returned center is a candidate hypothesis ONLY, never verified player.
    """
    a = np.asarray(logits, dtype=np.float64)
    xy = np.asarray(centers, dtype=np.int64)
    require(a.ndim == 1 and len(a) >= 3 and np.isfinite(a).all() and
            xy.shape == (len(a),2) and np.isfinite(xy).all(),
            "Nonfinite or wrong-sized full-image raw candidate scan.")
    winner = int(np.argmax(a))
    best = float(a[winner])
    separated = np.sum((xy-xy[winner])**2,axis=1) >= MIN_SEPARATION_PIXELS**2
    require(bool(separated.any()),"No separate image candidate: cannot estimate object dominance.")
    next_best = float(a[separated].max())
    p90 = float(np.quantile(a, BACKGROUND_QUANTILE))
    q75 = float(np.quantile(a, BACKGROUND_QUARTILE_HIGH))
    q25 = float(np.quantile(a, BACKGROUND_QUARTILE_LOW))
    background_contrast = (best-p90)/max(RAW_SCORE_SPREAD_FLOOR, q75-q25)
    object_dominance = (best-next_best)/max(RAW_SCORE_SPREAD_FLOOR, q75-q25)
    evidence = min(background_contrast,object_dominance)
    require(math.isfinite(evidence) and evidence >= -1e-8,
            "Bad image-relative source-only evidence.")
    return {"evidence":max(0.,float(evidence)), "center":tuple(int(x) for x in xy[winner]),
            "backgroundContrast":max(0.,float(background_contrast)),
            "objectDominance":max(0.,float(object_dominance)),
            "winnerIndex":winner}


def calibrate_absent(whole_absent_observations):
    """ONLY disjoint, truly human-ABSENT original screenshot evidence.

    Takes entries of (runId, originalScreenshotSHA256, evidenceScore).
    Never count several crops/frames with the same original PNG as independent.
    No conformal coverage or independently exchangeable probability claim.
    """
    rows=list(whole_absent_observations)
    source_hashes=set()
    runs=set()
    for run, source_sha, score in rows:
        require(type(run) is str and run.startswith("dev-") and
                type(source_sha) is str and len(source_sha)==64 and
                all(c in "0123456789abcdef" for c in source_sha) and
                source_sha not in source_hashes and
                type(score) in (float,int) and math.isfinite(score) and score>=0,
                "Absent calibration needs authentic distinct original screenshots.")
        source_hashes.add(source_sha)
        runs.add(run)
    require(len(rows)>=MIN_INDEPENDENT_ABSENT and len(runs)>=MIN_ABSENT_RUNS,
            "NOT_READY: need >=4 distinct whole-ABSENT originals across >=2 development runs.")
    return max(score for _,_,score in rows)+ABSENT_SCORE_MARGIN


def decide(evidence, frozen_threshold):
    """Strictly reject ties or insufficient evidence, no invented player location."""
    require(math.isfinite(frozen_threshold) and frozen_threshold>=ABSENT_SCORE_MARGIN and
            math.isfinite(evidence["evidence"]),
            "No genuine absence calibration or nonfinite evidence.")
    present = bool(evidence["evidence"] > frozen_threshold)
    return {"present":present,
            "center":evidence["center"] if present else None,
            "confidenceKind":"UNVALIDATED_IMAGE_RELATIVE_EVIDENCE_NOT_PROBABILITY",
            "evidence":evidence["evidence"]}


def whole_run_gate(reference, prediction):
    """Per-run evidence only; NEVER pool 3 runs to make one look good."""
    require(len(reference)==len(prediction),"Reference/prediction frame mismatch.")
    visible=absent=localized=fp=unknown=0
    for truth, pred in zip(reference,prediction):
        status=truth.get("state")
        require(status in ("visible","absent","unknown"), "Malformed independent reference.")
        if status=="unknown":
            unknown+=1
            continue
        if status=="absent":
            absent+=1
            fp+=int(pred["present"])
            continue
        visible+=1
        center=truth["center"]
        require(type(center) is dict and type(center.get("x")) is int and
                type(center.get("y")) is int and
                0<=center["x"]<390 and 0<=center["y"]<844,
                "Missing valid visible original-player coordinate.")
        if pred["present"]:
            x,y=pred["center"]
            localized+=int((x-center["x"])**2+(y-center["y"])**2 <= CENTER_TOLERANCE**2)
    ready=visible>=MIN_TRUE_VISIBLE_PER_HELD_RUN and absent>=MIN_TRUE_ABSENT_PER_HELD_RUN
    go=ready and localized/visible>=MIN_VISIBLE_CENTER_RECALL and fp/absent<=MAX_ABSENT_FALSE_PRESENCE
    return {"visible":visible, "absent":absent, "unknown":unknown,
            "correctLocationWithin35":localized,"falsePresenceOnAbsent":fp,
            "status":"INVESTIGATE_FURTHER" if go else "NO_GO" if ready else "NOT_READY"}
