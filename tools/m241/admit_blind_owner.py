#!/usr/bin/env python3
"""M2.41: strictly PRIVATE owner-label admission for frozen M2.38 originals.

Never infers a player, loads a game/DOM, trains on unreviewed images, publishes
owner coordinates or changes the active visual agent. All screenshots stay
local. Only aggregated counts are printed; caller retains exported JSON.
"""
import argparse
import json
from pathlib import Path

from tools.m238 import fresh_capture as source
from tools.m238.validate_archive import validate

REVIEW_FIELDS = frozenset(("schemaVersion", "studySeed", "sourceStudySeed",
                           "runId", "partition", "frames"))
FRAME_FIELDS = frozenset(("step", "screenshotSha256", "player"))
PLAYER_FIELDS = frozenset(("state", "center"))
COORD_FIELDS = frozenset(("x", "y"))
STATES = ("visible", "absent", "unknown")


def _unique_pairs(pairs):
    obj = {}
    for key, value in pairs:
        source.require(key not in obj, "Ambiguous duplicate key in private review JSON.")
        obj[key] = value
    return obj


def _read_json(path):
    source.require(path.is_file() and not path.is_symlink() and
                   0 < path.stat().st_size < 100_000,
                   "Missing/oversize/symlinked private review JSON.")
    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=_unique_pairs)


def _read_review(path, run, originals):
    data = _read_json(path)
    source.require(type(data) is dict and frozenset(data) == REVIEW_FIELDS and
                   data["schemaVersion"] == 1 and
                   data["studySeed"] == source.SEED and
                   data["sourceStudySeed"] == source.SEED and
                   data["runId"] == run and data["partition"] == "development",
                   "Wrong review run, frozen source seed or export schema.")
    frames = data["frames"]
    source.require(type(frames) is list and len(frames) == source.TARGETS,
                   "Incomplete original owner review export.")
    counts = {state: 0 for state in STATES}
    decisions = []
    for original, record in zip(originals, frames):
        source.require(type(record) is dict and frozenset(record) == FRAME_FIELDS and
                       type(record["step"]) is int and
                       record["step"] == original["step"] and
                       record["screenshotSha256"] == original["sha256"],
                       "Owner answer does not match frozen original SHA and step order.")
        player = record["player"]
        source.require(type(player) is dict and frozenset(player) == PLAYER_FIELDS and
                       type(player["state"]) is str and player["state"] in STATES,
                       "Player state missing or additional private export fields.")
        state, center = player["state"], player["center"]
        if state == "visible":
            source.require(type(center) is dict and frozenset(center) == COORD_FIELDS and
                           type(center["x"]) is int and type(center["y"]) is int and
                           0 <= center["x"] < 390 and 0 <= center["y"] < 844,
                           "VISIBLE requires an integer center in original 390x844 pixels.")
            truth = (state, center["x"], center["y"])
        else:
            source.require(center is None, "ABSENT and UNKNOWN must have null center.")
            truth = (state, None, None)
        counts[state] += 1
        decisions.append((original["sha256"], truth))
    return counts, decisions


def audit(sources: Path, review: Path, labels: Path | None = None):
    """Verify ALL original bytes first, THEN privately validate owner exports.

    Missing or UNKNOWN exports are permissible as progress, never as negative
    training truth. Identical source SHA can occur in different runs: conflicting
    definitive human labels are rejected without silently copying decisions.
    """
    integrity = validate(sources, review)
    manifest = _read_json(review / "study-manifest.json")
    # _read_json is bounded; M2.40 already checked the entire frozen file set.
    source.require(manifest["sourceSeed"] == source.SEED and
                   integrity["originalTargets"] == 54,
                   "Wrong source-backed M2.38 frozen review.")

    expected = {f"{run}-player-points.json" for run in source.RUNS}
    present = set()
    if labels is not None:
        source.require(labels.is_dir() and not labels.is_symlink(),
                       "Private owner label directory missing.")
        present = {p.name for p in labels.iterdir()}
        source.require(present <= expected and
                       all((labels / name).is_file() and
                           not (labels / name).is_symlink() for name in present),
                       "Private label directory contains unrecognized files or symlinks.")

    states = {}
    definitive_by_sha = {}
    duplicate_observations = 0
    labelled = 0
    for run, original in zip(source.RUNS, manifest["runs"]):
        filename = f"{run}-player-points.json"
        if filename not in present:
            states[run] = {"exportReceived": False, "visible": 0,
                           "absent": 0, "unknown": source.TARGETS}
            continue
        counts, frames = _read_review(labels / filename, run, original["images"])
        labelled += 1
        states[run] = {"exportReceived": True, **counts}
        for digest, truth in frames:
            if digest in definitive_by_sha:
                duplicate_observations += 1
                if truth[0] != "unknown" and definitive_by_sha[digest][0] != "unknown":
                    source.require(truth == definitive_by_sha[digest],
                                   "Conflicting definitive human truth for identical original PNG SHA.")
                if definitive_by_sha[digest][0] == "unknown" and truth[0] != "unknown":
                    definitive_by_sha[digest] = truth
            else:
                definitive_by_sha[digest] = truth

    unknown = sum(x["unknown"] for x in states.values())
    status = ("NOT_READY_NO_PRIVATE_HUMAN_LABELS" if labelled == 0 else
              "NOT_READY_PARTIAL_OWNER_EXPORTS" if labelled < len(source.RUNS) else
              "NOT_READY_UNKNOWN_OWNER_LABELS" if unknown else
              "PRIVATE_BLIND_DEVELOPMENT_REFERENCES_ADMITTED")
    return {
        "schemaVersion": 1, "milestone": "M2.41",
        "status": status, "studySeed": source.SEED,
        "sourceRunsIntegrity": integrity["status"],
        "originalTargets": integrity["originalTargets"],
        "uniqueOriginalDigests": integrity["uniqueTargetDigests"],
        "originalPredecessors": integrity["authenticPredecessors"],
        "ownerExportsReceived": labelled, "ownerExportsRequired": len(source.RUNS),
        "reviewedObservations": sum(row["visible"] + row["absent"]
                                    for row in states.values()),
        "unknownObservations": unknown,
        "duplicateObservationsCompared": duplicate_observations,
        "runs": states,
        "humanOriginIndependentVerification": False,
        "sourceRole": "DEVELOPMENT_DIAGNOSTIC_NOT_INDEPENDENT_FIT_OR_CALIBRATION",
        "m237FullOriginalSequenceReady": False,
        "liveAgentChanged": False,
        "playerIdentityAccuracyEvaluated": False,
        "ownerCoordinatesIncluded": False,
    }


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--sources", required=True, type=Path,
                   help="Unpacked ORIGINAL historical source ZIP; never GitHub Pages")
    p.add_argument("--review", required=True, type=Path,
                   help="Unpacked frozen original-only private reviewer ZIP")
    p.add_argument("--labels", type=Path,
                   help="Separate PRIVATE folder with exported m238-*-player-points.json")
    a = p.parse_args()
    print(json.dumps(audit(a.sources, a.review, a.labels), indent=2))


if __name__ == "__main__":
    main()
