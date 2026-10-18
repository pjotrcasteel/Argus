#!/usr/bin/env python3
"""Private, source-verified M2.27 human review of candidate proposals, never player identity.

Use only with authentic original development R/S/T source ZIP and locally reviewed
JSON. Never upload labels, predecessor frames, result details, or model weights.
No candidate proposal or tracker state is an ARGUS recognized player.
"""
import argparse
import hashlib
import io
import json
import re
import struct
from pathlib import Path
from zipfile import ZipFile

import numpy as np
from PIL import Image

from tools.m227.temporal_candidates import analyze_pair

FROZEN_ARCHIVE_SHA256 = "bdb8538c90ccb5166fcee4b94e9f46e6c4ba1a91d70079c370c4c13d65173bda"
FROZEN_STUDY_SEED = "argus-m227-temporal-context-rst-20261018-v1"
RUNS = ("dev-r", "dev-s", "dev-t")
PNG_MAGIC = b"\x89PNG\r\n\x1a\n"
WIDTH, HEIGHT, THRESHOLD = 390, 844, 35


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def require(condition, reason):
    if not condition:
        raise ValueError(reason)


def image_bytes(raw, expected_sha):
    require(len(raw) >= 24 and raw[:8] == PNG_MAGIC and
            struct.unpack(">II", raw[16:24]) == (WIDTH, HEIGHT) and
            digest(raw) == expected_sha, "Original screenshot or SHA was changed.")
    with Image.open(io.BytesIO(raw)) as image:
        rgb = np.asarray(image.convert("RGB"), dtype=np.uint8)
    require(rgb.shape == (HEIGHT, WIDTH, 3), "Decoded source image shape invalid.")
    return rgb


def verify_player(player):
    require(type(player) is dict and player.get("state") in ("visible", "absent", "unknown"),
            "Human frame must be explicitly visible / absent / unknown.")
    state, center = player["state"], player.get("center")
    if state == "visible":
        require(type(center) is dict and type(center.get("x")) is int and
                type(center.get("y")) is int and 0 <= center["x"] < WIDTH and
                0 <= center["y"] < HEIGHT, "Visible player requires original-pixel center.")
    else:
        require(center is None, "No invented coordinate on absent / unknown images.")


def evaluate(archive: Path, labels_dir: Path, *, expected_archive_sha=FROZEN_ARCHIVE_SHA256,
             analyzer=analyze_pair, private_details=False):
    require(archive.is_file() and 0 < archive.stat().st_size < 30_000_000 and
            digest(archive.read_bytes()) == expected_archive_sha,
            "Wrong frozen original-source archive SHA; fail closed.")
    results = []
    seen_source_truth = {}
    with ZipFile(archive) as z:
        names = z.namelist()
        require(len(names) == len(set(names)) == 113 and z.testzip() is None and
                all(not n.startswith("/") and ".." not in Path(n).parts for n in names),
                "Source archive membership, CRC or path integrity failed.")
        manifest = json.loads(z.read("study-manifest.json"))
        require(manifest.get("schemaVersion") == 1 and manifest.get("seed") == FROZEN_STUDY_SEED and
                manifest.get("runIds") == list(RUNS) and manifest.get("targetsPerRun") == 18 and
                manifest.get("originalTargetImages") == 54 and
                manifest.get("originalPrecedingContextImages") == 54 and
                manifest.get("prospectiveOriginalsAccessed") is False and
                len(manifest.get("runs", [])) == 3,
                "Wrong approved original-development protocol.")
        all_paths = {"study-manifest.json", "index.html"}
        for source, run in zip(manifest["runs"], RUNS):
            require(source.get("runId") == run and len(source.get("images", [])) == 18 and
                    len(source.get("immediatePriorOriginalFrames", [])) == 18,
                    "Run order or genuine original target/context pairing changed.")
            file = labels_dir / f"m227-{run}-player-points.json"
            require(file.is_file(), "One of the three locally human-reviewed JSON files is missing.")
            label = json.loads(file.read_text(encoding="utf-8"))
            require(label.get("schemaVersion") == 1 and label.get("studySeed") == FROZEN_STUDY_SEED and
                    label.get("sourceStudySeed") == FROZEN_STUDY_SEED and
                    label.get("runId") == run and label.get("partition") == "development" and
                    len(label.get("frames", [])) == 18,
                    "Wrong review seed, run, partition or frame count; refuse mismatched truth.")
            all_paths.add(f"annoteren/{run}/index.html")
            records = []
            last_selected_step = None
            last_tracks = ()
            for frozen, prior, human in zip(source["images"], source["immediatePriorOriginalFrames"], label["frames"]):
                step = frozen.get("step")
                require(type(step) is int and step >= 1 and step <= 180 and
                        frozen.get("file") == f"frame-{step:04d}.png" and
                        human.get("step") == step and human.get("screenshotSha256") == frozen.get("sha256") and
                        prior.get("targetStep") == step and prior.get("priorStep") == step - 1 and
                        prior.get("file") == f"context/{run}/prev-for-{step:04d}.png" and
                        re.fullmatch(r"[0-9a-f]{64}", frozen["sha256"]) and
                        re.fullmatch(r"[0-9a-f]{64}", prior["sha256"]) and
                        (last_selected_step is None or step > last_selected_step),
                        "Review image step/sha or temporal source chronology differs.")
                verify_player(human.get("player"))
                signature = json.dumps(human["player"], sort_keys=True)
                old = seen_source_truth.setdefault(frozen["sha256"], signature)
                require(signature == old, "Identical source PNG cannot have contradictory human review.")
                target_name = f"annoteren/{run}/{frozen['file']}"
                all_paths.update((target_name, prior["file"]))
                current = image_bytes(z.read(target_name), frozen["sha256"])
                previous = image_bytes(z.read(prior["file"]), prior["sha256"])
                adjacent = last_selected_step is not None and step == last_selected_step + 1
                # A gap in selected frames is not an observed chronological track.
                evidence, last_tracks = analyzer(previous, current, step, last_tracks if adjacent else ())
                last_selected_step = step
                require(evidence.get("status") == "SOURCE_ONLY_CANDIDATES_NOT_PLAYERS" and
                        evidence.get("step") == step and len(evidence.get("proposals", [])) <= 16,
                        "Unrecognized or excessive source-only proposals.")
                proposals = evidence["proposals"]
                state, center = human["player"]["state"], human["player"]["center"]
                distances = ([((p["x"]-center["x"])**2 + (p["y"]-center["y"])**2)**.5
                              for p in proposals] if state == "visible" else [])
                match_ranks = [i+1 for i, d in enumerate(distances) if d <= THRESHOLD]
                nearest_rank = (min(range(len(distances)), key=lambda i: distances[i])+1 if distances else None)
                records.append({"step": step, "sourceSha256": frozen["sha256"],
                                "humanState": state, "proposalCount": len(proposals),
                                "registrationReliable": evidence["registration"]["reliable"],
                                "cameraShift": abs(evidence["registration"]["dx"]) +
                                               abs(evidence["registration"]["dy"]),
                                "hit35pxTop16": bool(match_ranks),
                                "firstHitRank": min(match_ranks) if match_ranks else None,
                                "nearestCandidateRank": nearest_rank,
                                "nearestCandidatePixels": round(min(distances), 3) if distances else None,
                                "fixedCenterHit35px": (abs(center["x"]-195)**2 +
                                                       abs(center["y"]-422)**2 <= THRESHOLD**2)
                                                      if state == "visible" else None,
                                "adjacentReviewedFrames": adjacent})
            visible = [row for row in records if row["humanState"] == "visible"]
            absent = [row for row in records if row["humanState"] == "absent"]
            hits = sum(row["hit35pxTop16"] for row in visible)
            absent_burden = sum(row["proposalCount"] > 0 for row in absent)
            gate = ("NOT_EVALUATED" if not visible or not absent else
                    "INVESTIGATE_FURTHER" if hits*10 >= 7*len(visible) and
                    absent_burden*10 < 3*len(absent) else "NO_GO")
            result = {"runId": run, "visibleReviewed": len(visible), "absentReviewed": len(absent),
                      "unknownReviewed": 18-len(visible)-len(absent),
                      "top16PlayerCenterWithin35": hits,
                      "top1PlayerCenterWithin35": sum(r["firstHitRank"] == 1 for r in visible),
                      "centerAlwaysWithin35": sum(r["fixedCenterHit35px"] for r in visible),
                      "fixedCenterFalsePresenceOnAbsent": len(absent),
                      "absentFramesWithCandidates": absent_burden,
                      "candidateTotalOnAbsent": sum(r["proposalCount"] for r in absent),
                      "registeredFrames": sum(r["registrationReliable"] for r in records),
                      "nonzeroCameraShiftFrames": sum(r["cameraShift"] > 0 for r in records),
                      "candidateEmittingFrames": sum(r["proposalCount"] > 0 for r in records),
                      "adjacentReviewedPairs": sum(r["adjacentReviewedFrames"] for r in records),
                      "firstHitRanks": [r["firstHitRank"] for r in visible if r["firstHitRank"]],
                      "gate": gate}
            if private_details:
                # Per-image source SHA/ranks remain local; no owner coordinates included.
                result["PRIVATE_PER_IMAGE_DETAIL_NEVER_COMMIT"] = records
            results.append(result)
        require(set(names) == all_paths, "Extra source materials in ZIP: strict allowlist rejected.")
    return {"schemaVersion": 1, "status": "PRIVATE_DEVELOPMENT_ONLY_NO_PLAYER_CLASSIFIER",
            "originalArchiveSha256": expected_archive_sha,
            "sourceStudySeed": FROZEN_STUDY_SEED,
            "uniqueSourceScreenshotDigests": len(seen_source_truth),
            "runs": results,
            "overallGate": "INVESTIGATE_FURTHER" if all(r["gate"] == "INVESTIGATE_FURTHER" for r in results)
                           else "NOT_EVALUATED" if any(r["gate"] == "NOT_EVALUATED" for r in results)
                           else "NO_GO"}


def main():
    parser = argparse.ArgumentParser(description="STRICTLY PRIVATE human-grounded M2.27 source-only audit")
    parser.add_argument("--original-archive", required=True, type=Path)
    parser.add_argument("--labels-dir", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path, help="Private local JSON path, NEVER a GitHub workspace upload")
    parser.add_argument("--private-details", action="store_true", help="Include private frame-step diagnostics")
    args = parser.parse_args()
    report = evaluate(args.original_archive, args.labels_dir, private_details=args.private_details)
    require(not args.output.exists(), "Refuse to silently overwrite frozen research results.")
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("PRIVATE M2.29:", report["overallGate"],
          "(SOURCE-ONLY; candidate hits are NOT recognized players; NEVER upload owner data)")
    for run in report["runs"]:
        print(run["runId"], "top16", run["top16PlayerCenterWithin35"], "/", run["visibleReviewed"],
              "absent with candidates", run["absentFramesWithCandidates"], "/", run["absentReviewed"],
              "gate", run["gate"])


if __name__ == "__main__":
    main()
