# ARGUS M2.5c — Shadow-only screen-edge threat research

## Purpose

The owner manually reviewed 12 frames from a frozen, real WyrmForge **development** run (`run-02`). Seven combat frames have trustworthy original detection evidence; separate frame 26 is a transition with no detector invocation, and legacy bounded last frame 42 copied evidence from frame 41. Historic 26-pixel matching on the seven usable combat frames gave 27 TP / 2 FP / 11 FN, with six human-marked misses outside the original hard search rectangle.

These numbers **do not measure the new detector**. This milestone introduces a *read-only, paired A/B experiment*, not a new gameplay algorithm.

## Implementation

`CombatPixelPerception.ObserveShadow` performs two interpretations of one screenshot:

1. **Baseline:** call the unchanged active `ObserveRgb` method; its player recognition and original 19%–82% central-world threat detection are not modified.
2. **Shadow proposal:** find supplemental palette-matching regions between 11% and 93% of height and across the viewport width. Keep only candidates whose centers are outside the original threat-search bounds, de-duplicate them against the baseline, reject candidates too close to the visually recognized player, and cap confidence at 0.50. We retain the old size/pixel thresholds. The top 11% HUD and bottom 7% footer remain excluded.

This deliberately conservative probe **can still detect HUD elements, scenery and particles**. A pixel palette alone cannot establish enemy identity. Its results are never passed into `VisualCombatSteering` or any active WyrmForge controls.

## Run the original human-label A/B locally

Recover/extract the original real research ZIP into a **local** folder. Place the original human-reviewed `labels.json` into the extracted **run-02** directory; leave the held-out **run-01** untouched. Run from the repository root:

```bash
dotnet run --project src/Argus.Evaluation.Tool -- --run results/research/run-02 --shadow-edges examples/wyrmforge.profile.json
```

This produces `run-02/shadow-edge-study.json` and `run-02/shadow-edge-study.md` next to the screenshots. No game input, remote service, extra human labels or uploads are required.

The report compares the **fresh baseline** and **fresh expanded probe** on the **same original image** with the same player bootstrap settings (without carrying a tracking prior between sampled frames), using the same deterministic one-to-one `ThreatMatching` code used elsewhere. Thus its baseline numbers may **not equal** the frozen original trace metrics, which used running temporal player context. Treat the experiment as a controlled screening comparison, not an exact replay of earlier decisions.

Only frames that are genuinely labeled, in the exploring phase, with a freshly recognized player are scored. Unknown reference labels, transitions and older copied final-frame evidence remain explicitly unscored. Reports show the denominators and uncertainties: no annotations are inferred or overwritten.

## Promotion gate

Do **not** make the new detections steer the active agent on the strength of seven correlated development frames. First, inspect any additional false positives visually and require improvement across independent development runs. Then collect fresh unseen run-level held-out screenshots, use their labels once for the final locked comparison, and verify full real-game behavior. Preserve original labels/traces locally, never push human review datasets into the public source repository.
