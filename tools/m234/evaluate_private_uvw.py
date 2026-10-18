#!/usr/bin/env python3
"""M2.34: verify original U/V/W owner truth and score PRE-FROZEN M2.32 offline.

STRICTLY PRIVATE: only local original 113-member dev-only archive + private
owner exports. Never upload original context frames, human coordinate files,
per-frame details or learned coefficients to GitHub/Pages/Actions.
No sealed O/P/Q or old run-01; NEVER enables live game steering.
"""
import argparse
import hashlib
import io
import json
import math
import re
import sys
from pathlib import Path
from zipfile import ZipFile

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from tools.m230 import identity_study as appearance
from tools.m232 import relative_presence as frozen

ARCHIVE_SHA256 = "ab1c338aea8af582b0c93de2f7c7b4f11528b62c50cf03ce1fd3e013dff7e26f"
STUDY_SEED = "argus-m232-independent-uvw-development-20261018-v1"
RUNS = ("dev-u", "dev-v", "dev-w")
PNG_HEADER = b"\x89PNG\r\n\x1a\n"
ORIGINAL_DIMENSIONS = bytes.fromhex("000001860000034c")


def require(ok, why):
    if not ok:
        raise ValueError(why)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def original_rgb(data, expected_sha):
    require(24 <= len(data) <= 2_000_000 and data[:8] == PNG_HEADER and
            data[16:24] == ORIGINAL_DIMENSIONS and digest(data) == expected_sha,
            "Original source screenshot or preceding image SHA, format or size differs.")
    with Image.open(io.BytesIO(data)) as image:
        pixels = np.asarray(image.convert("RGB"), dtype=np.uint8)
    require(pixels.shape == (844, 390, 3),
            "Original source screenshot decoded dimensions differ.")
    return pixels


def admit_original_reviews(archive, private_labels_dir, *, expected_archive_sha=ARCHIVE_SHA256):
    require(archive.is_file() and 0 < archive.stat().st_size < 30_000_000 and
            digest(archive.read_bytes()) == expected_archive_sha,
            "Wrong SHA-pinned genuine original U/V/W ZIP; refuse unverifiable owner truth.")
    rows = {}
    source_truth = {}
    with ZipFile(archive) as z:
        filenames = z.namelist()
        require(len(filenames) == len(set(filenames)) == 113 and
                all(not p.startswith("/") and ".." not in Path(p).parts and
                    not p.endswith("/") for p in filenames) and z.testzip() is None,
                "Forbidden source/archive member or corrupt ZIP; fail closed.")
        manifest = json.loads(z.read("study-manifest.json"))
        require(manifest.get("schemaVersion") == 1 and manifest.get("seed") == STUDY_SEED and
                manifest.get("sourceRole") == "independent-development-source-only" and
                manifest.get("runIds") == list(RUNS) and manifest.get("targetsPerRun") == 18 and
                manifest.get("originalTargetImages") == 54 and
                manifest.get("originalPrecedingContextImages") == 54 and
                manifest.get("humanGroundTruth") == "ALL_UNKNOWN" and
                manifest.get("prospectiveOriginalsAccessed") is False and
                len(manifest.get("runs", [])) == len(RUNS),
                "Not the frozen source-only, development-only U/V/W study.")
        expected_members = {"study-manifest.json", "index.html"}
        for run, original in zip(RUNS, manifest["runs"]):
            require(original.get("runId") == run and
                    len(original.get("images", [])) == 18 and
                    len(original.get("immediatePriorOriginalFrames", [])) == 18,
                    "Original whole-run order or source target/context count changed.")
            private = private_labels_dir / f"m232-{run}-player-points.json"
            require(private.is_file(), "Owner original mobile review missing: " + run)
            labels = json.loads(private.read_text(encoding="utf-8"))
            require(labels.get("schemaVersion") == 1 and
                    labels.get("studySeed") == STUDY_SEED and
                    labels.get("sourceStudySeed") == STUDY_SEED and
                    labels.get("runId") == run and
                    labels.get("partition") == "development" and
                    len(labels.get("frames", [])) == 18,
                    "Private owner-reviewed source study/run/seed/partition differs.")
            expected_members.add(f"annoteren/{run}/index.html")
            frames = []
            previous_step = -1
            for target, preceding, human in zip(original["images"],
                                                 original["immediatePriorOriginalFrames"],
                                                 labels["frames"]):
                step = target.get("step")
                require(type(step) is int and previous_step < step <= 180 and step >= 1 and
                        target.get("file") == f"frame-{step:04d}.png" and
                        preceding.get("targetStep") == step and
                        preceding.get("priorStep") == step-1 and
                        preceding.get("file") == f"context/{run}/prev-for-{step:04d}.png" and
                        human.get("step") == step and
                        human.get("screenshotSha256") == target.get("sha256") and
                        re.fullmatch("[a-f0-9]{64}", target["sha256"]) and
                        re.fullmatch("[a-f0-9]{64}", preceding["sha256"]),
                        "Owner reviewed wrong original step or target/context screenshot SHA.")
                previous_step = step
                shot = f"annoteren/{run}/{target['file']}"
                prior = preceding["file"]
                expected_members.update((shot, prior))
                pixels = original_rgb(z.read(shot), target["sha256"])
                original_rgb(z.read(prior), preceding["sha256"])
                player = human.get("player")
                require(type(player) is dict and player.get("state") in
                        ("visible", "absent", "unknown"),
                        "Owner-visible original source truth is missing or invalid.")
                state, center = player["state"], player.get("center")
                if state == "visible":
                    require(type(center) is dict and type(center.get("x")) is int and
                            type(center.get("y")) is int and
                            0 <= center["x"] < 390 and 0 <= center["y"] < 844,
                            "Human VISIBLE requires valid original-pixel player center.")
                else:
                    require(center is None, "Owner ABSENT/UNKNOWN cannot fabricate player center.")
                truth = (state, json.dumps(center, sort_keys=True))
                old = source_truth.setdefault(target["sha256"], truth)
                require(old == truth, "Contradictory human truth on identical original screenshot.")
                frames.append({"run":run, "step":step, "sha":target["sha256"],
                               "state":state, "center":center, "rgb":pixels})
            rows[run] = frames
        require(set(filenames) == expected_members,
                "Unapproved extra source, original prospective, owner data or trace.")
    return rows, {"originalSourceSha256":expected_archive_sha, "studySeed":STUDY_SEED,
                  "reviewedObservations":54, "uniqueImageSha256":len(source_truth),
                  "runStateCounts":{r:{s:sum(x["state"]==s for x in rows[r])
                                       for s in ("visible","absent","unknown")}
                                    for r in RUNS}}


def evaluate_frozen(by_run, *, grid=appearance.GRID, model_fit=appearance.fit_model):
    require(set(by_run) == set(RUNS), "Missing independently reviewed whole development source run.")
    folds = []
    private_details = []
    for held in RUNS:
        split = frozen.split_human_development_runs(by_run, held)
        model = model_fit(split["fit"])
        scores = {}
        def measure(sample):
            if sample["sha"] not in scores:
                patch_features = appearance.features(sample["rgb"], grid)
                logits = ((patch_features-model["mean"])/model["std"]) @ model["weights"] + model["bias"]
                scores[sample["sha"]] = frozen.image_evidence(logits, grid)
            return scores[sample["sha"]]
        control = [(f["run"], f["sha"], measure(f)["evidence"]) for f in split["calibration"]]
        threshold = frozen.calibrate_absent(control)
        held_images = split["heldOut"]
        preds = [frozen.decide(measure(f), threshold) for f in held_images]
        truth = [{"state":f["state"], "center":f["center"]} for f in held_images]
        metrics = frozen.whole_run_gate(truth, preds)
        always_center = frozen.whole_run_gate(
            truth, [{"present":True, "center":(195,422)} for _ in held_images])
        near = sum(f["state"] == "visible" and
                   math.dist(measure(f)["center"], (f["center"]["x"],f["center"]["y"])) <= 35
                   for f in held_images)
        weights_hash = digest(appearance.canonical({
            "coefficients":model["weights"].tolist(), "normalizationMean":model["mean"].tolist(),
            "normalizationStd":model["std"].tolist(), "bias":model["bias"],
            "threshold":threshold, "fitOriginalImageSHAs":sorted(f["sha"] for f in split["fit"]),
            "calibrationOriginalImageSHAs":sorted(f["sha"] for f in split["calibration"]])}).encode())
        folds.append({"testRun":held, "fitVisible":sum(f["state"]=="visible" for f in split["fit"]),
                      "fitAbsent":sum(f["state"]=="absent" for f in split["fit"]),
                      "calibrationDistinctTrueAbsent":len(split["calibration"]),
                      "calibrationSourceRuns":sorted({f["run"] for f in split["calibration"]}),
                      "testDistinctOriginals":len(held_images),
                      "excludedDuplicateImageSHA":split["sharedExactSourceShaExcluded"],
                      "metrics":metrics, "alwaysCenterFalsePresenceBaseline":always_center,
                      "rawTopCandidateNearTruePlayer":near,
                      "privateFrozenThreshold":threshold,
                      "privateFittedModelSHA256":weights_hash})
        private_details.extend({"run":held, "step":f["step"], "originalSha256":f["sha"],
                                "humanState":f["state"], "humanCenter":f["center"],
                                "hypotheticalTopCenter":list(measure(f)["center"]),
                                "evidence":measure(f)["evidence"],
                                "frozenThreshold":threshold, "predictedPresent":p["present"],
                                "acceptedCenter":p["center"]}
                               for f,p in zip(held_images, preds))
    return {"schemaVersion":1,
            "protocol":frozen.MODEL_HYPOTHESIS,
            "status":"PRIVATE_DEVELOPMENT_ONLY_NO_GO_LIVE_UNCHANGED",
            "wholeRunOutFolds":folds,
            "allInvestigateFurther":all(x["metrics"]["status"]=="INVESTIGATE_FURTHER"
                                       for x in folds),
            "prospectiveOriginalsOpened":False, "liveAgentChanged":False,
            "PRIVATE_FRAME_DETAILS_NEVER_COMMIT":private_details}


def main():
    parser = argparse.ArgumentParser(description="Original authenticated PRIVATE M2.34 U/V/W player reference test")
    parser.add_argument("--original-review-zip", required=True, type=Path)
    parser.add_argument("--owner-labels-dir", required=True, type=Path)
    parser.add_argument("--private-report", required=True, type=Path)
    args = parser.parse_args()
    require(not args.private_report.exists(), "Refuse to overwrite a previously frozen private evaluation.")
    source, admitted = admit_original_reviews(args.original_review_zip, args.owner_labels_dir)
    report = {"admission":admitted, **evaluate_frozen(source)}
    args.private_report.write_text(json.dumps(report, indent=2)+"\n", encoding="utf-8")
    for fold in report["wholeRunOutFolds"]:
        score = fold["metrics"]
        print(fold["testRun"], score["correctLocationWithin35"], "/", score["visible"],
              "false on absent", score["falsePresenceOnAbsent"], "/", score["absent"],
              "status", score["status"])
    print("PRIVATE M2.34:", "NO LIVE PROMOTION; source-image and owner data NEVER upload to GitHub.")


if __name__ == "__main__":
    main()
