# M2.30 — offline learned player appearance and strict whole-run-out rejection

**Status:** experimental DEVELOPMENT evaluation. Not a validated player detector.
The active game agent and all WyrmForge source/DOM/telemetry remain unchanged.

## Motivation and hypothesis

The owner's original R/S/T reference evaluation in M2.29 conclusively failed
the M2.27 source-only temporal-proposal coverage gate. Many human-visible
players lacked any foreground-motion proposal and static UI could outrank the
player. It is inappropriate to relax the M2.27 candidate cutoff until it
passes. M2.30 therefore tests a **separate**, standalone, learnable image-only
identity hypothesis against authentic development screenshots, not a new
live-game feature.

This is a **locally trained**, class-balanced, regularized visual appearance
model. It is not a general pretrained foundation model, reinforcement learning,
or autonomous skill adaptation. It estimates a patch score and may abstain
rather than inventing a location.

## Frozen methodology within this development experiment

The code in `tools/m230/identity_study.py` defines:

1. Original 390×844 RGB, 48×48 pixel patch around the candidate center, with
   edge-repeat padding so x=0 and x=389 remain valid. No x/y, screen center,
   game-specific palette, sprites, actions, phases or label coordinates enter
   the **descriptor**.
2. Downsample to 12×12 relative luminance values, 6×6 RGB means and 6×6
   local grayscale gradient magnitude averages: **288 feature dimensions**.
   Shape/appearance alone cannot prove player identity.
3. Scan the **entire original screenshot** at 16px intervals including
   all four screen boundaries. Training positives are the exact reviewed
   player-center crops; negatives are 63 fixed position-diverse crops per
   image with a **72px exclusion** around verified visible player centers.
   Actual entirely human-ABSENT images provide genuine negative controls.
4. Train a deterministic class-balanced L2 logistic model using precisely
   350 full-batch steps, learning rate 0.12 and L2 0.15. Only fitting-frame
   features calculate normalization; no validation or prospective pixels
   enter model weights or mean/std statistics.
5. Whole-source-run-out R/S/T development cross-fit. Exclude **every exact
   test image SHA** from both other runs' fit and absence calibration before
   deduplicating, even if the same image appears at a different step.
   Within each fit run, chronology-ordered deduplicated ABSENT screenshots
   are alternated into calibration (indices 0,2,...) and fitting.
   Calibration needs **at least three independent ABSENT original PNGs**;
   otherwise fail with NOT_READY, never pretend background patches equal
   independent absent screenshots.
6. Threshold = maximum whole-screen fitted score across the strictly
   out-of-fit human-ABSENT calibration set **plus 0.05 raw logit**. The top
   whole-image score must exceed it, otherwise answer player ABSENT. This is
   a conservative empirical heuristic; three correlated real absences
   DO NOT provide a calibrated error bound.
7. Compare each held-run result to the weak always-center baseline, with
   distinct **visible within 35px** / **false presence on ABSENT** counts.
   The exploratory go gate requires >=50% visible location recall AND <=25%
   absent false presence **per run**, at least 8 visible and 2 absent.
   UNKNOWN is excluded rather than fabricated; exact duplicate frames
   counted once in M2.30's unique-image metrics.
8. Model normalization, coefficients, threshold, source input SHA and
   model hashes remain **private**, never inserted into Git, Actions,
   Pages, issues, example fixtures or live steering.

These choices were written after M2.29's original R/S/T failure was known.
Consequently the R/S/T cross-fit is DEVELOPMENT research only, not a fully
untouched preregistered independent evaluation. Even a passing result
would require a separately sealed and one-shot prospective assessment.

## Exact source and private evaluation

Use ONLY the source SHA-pinned `M2.27` 113-entry original ZIP, ID
`12137923805` and ZIP SHA256
`bdb8538c90ccb5166fcee4b94e9f46e6c4ba1a91d70079c370c4c13d65173bda`.
The 54 screenshot targets, 54 original immediate predecessors, and all
human reviews are reverified by the M2.29 admission routine before training.

Keep these owner-reviewed exports in a private, non-repository directory:
`m227-dev-r-player-points.json`, `m227-dev-s-player-points.json`,
`m227-dev-t-player-points.json`.

```bash
python -m pip install "numpy>=2,<3" "Pillow>=11,<13"
OPENBLAS_NUM_THREADS=1 PYTHONPATH=. python tools/m230/identity_study.py \
  --original-archive /private/argus_m227_original_pairs_private.zip \
  --labels-dir /private/argus-owner-reviews \
  --output /private/m230-development-result.json
```

The script refuses to overwrite an existing private report. CI only uses
artificial generated 390×844 RGB arrays and labels. Owner files, actual
screenshots, true locations, private fold metrics and model weights are not
placed into source control or uploaded as workflow artifacts.

## Verified actual development outcome

The exact SHA-verified R/S/T owner-local cross-fit has been run independently
of the original M2.27 proposal mechanism. The resulting learned image model
found more real player centers in development than the failed motion proposal
generator, but **all three development folds failed** the per-run gate, and
the always-center baseline still localizes more visible examples (while
falsely claiming presence on ABSENT frames).

**NO-GO.** This experimental appearance model must not control ARGUS.
Small, correlated training data, non-independent frame occurrences, calibration
with just three true ABSENT examples per fold and prominent UI/enemy distractors
remain important scientific limitations. No player identity, winning gameplay,
combat perception or learnability generalization is established.

## Next focused vertical slice (M2.31)

Inspect mistakes on held-out **development** frames privately, categorize
`missedVisible`, `presentButWrongLocation` and `falsePresence` without
editing already reported M2.30 thresholds. Prioritize independent player
appearance from identical-looking threats, HUD decorations, and edge clipping.
Design a *new* learning representation with a distinct genuinely unreviewed
development capture/annotation cohort or use held-out control only after an
explicit immutable pretrained model freeze. Preserve original unopened
O/P/Q prospective evidence (archived original expires October 25) and M2.4
`run-01` untouched. An additional model is not automatically progress:
require run-level hard-absence rejection and true location quality before
any new live input can be authorized.
