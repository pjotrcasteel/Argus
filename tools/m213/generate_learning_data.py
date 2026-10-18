#!/usr/bin/env python3
"""Synthetic ONLY logistic research fixture. Never a game accuracy/trained AI claim."""
import csv
from pathlib import Path

base = Path("artifacts/ci-learning-synthetic")
partial = Path("artifacts/ci-learning-insufficient")
base.mkdir(parents=True, exist_ok=True)
partial.mkdir(parents=True, exist_ok=True)
header = ["run_id", "step", "raw_bridge", "player_palette_pixels", "max_rgb_stddev",
          "strong_edge_fraction", "witness", "reviewed_player_label"]


def create(root, run_id, offset):
    folder = root / run_id
    folder.mkdir(parents=True, exist_ok=True)
    with (folder / "player-shadow-feature-rows.csv").open("w", newline="") as fd:
        writer = csv.writer(fd)
        writer.writerow(header)
        for index in range(5):
            writer.writerow([run_id, index, "BRIDGE_ONE", 60 + offset * 4 + index * 6,
                             20 + index * 3, round(.18 + index * .01, 5), "INDETERMINATE", 1])
        for index in range(3):
            writer.writerow([run_id, index + 5, "BRIDGE_TWO", 1400 if index == 2 else 0,
                             36 + offset * 2, .05 if index == 2 else .65, "INDETERMINATE", 0])
        for index in range(2):
            writer.writerow([run_id, index + 8, "BRIDGE_ONE", 0, 0, 0, "EMPTY_PATCH_VETO", 0])
        for index in range(2):
            writer.writerow([run_id, index + 10, "BRIDGE_ONE", 0, 0, 0, "EMPTY_PATCH_VETO", ""])


for run, offset in (("dev-a", 0), ("dev-b", 1), ("dev-c", 2)):
    create(base, run, offset)
for run, offset in (("dev-a", 0), ("dev-b", 1)):
    create(partial, run, offset)

# Poison remains unmodified, unread. This is an actual held-out exclusion assertion:
# if accidentally enumerated the model would either fail or silently contaminate its run counts.
holdout = base / "run-01"
holdout.mkdir(exist_ok=True)
(holdout / "player-shadow-feature-rows.csv").write_text("DO NOT READ / HELD OUT\n")

print("SYNTHETIC development-only training cohorts created: 3 sufficient, 2 insufficient.")
