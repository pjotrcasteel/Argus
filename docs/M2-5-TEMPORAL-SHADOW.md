# ARGUS M2.5d — Causal temporal confirmation (shadow only)

This milestone is an **offline research filter**, not gameplay code. M2.5c's independent development-run probe found that widening the original pixel search region may recover visible threats at the cost of many extra decoration/particle candidates: a separate approximate local implementation produced baseline 26 TP / 4 FP / 11 FN, raw shadow 30 TP / 13 FP / 7 FN on six labeled screenshots. Those numbers are **not official .NET scores** and remain one correlated development session.

## Three-way comparison

`Argus.Evaluation.Tool --run <local run-02> --shadow-edges <profile.json>` now reports, for each scored human-labeled screenshot:

- **Baseline:** the exact active `CombatPixelPerception.ObserveRgb` on the current PNG.
- **Raw shadow:** the baseline plus the M2.5c extra edge/top/bottom visual candidates on that PNG.
- **Temporal shadow:** the baseline plus only extra candidates matching a *previous contiguous combat screenshot* within 55 pixels and no more than a 2x width/height change. Each candidate needs at least **two consecutive observations** and at least **3 pixels cumulative observed movement** before confirmation.

Use *all* recorded frames in chronological order for candidate association, including those without human labels. Only preselected human-reviewed steps are scored. No candidate can be confirmed by its future appearance. A noncombat frame, missing player, or untrusted legacy final-frame resets the tracker; overlapping tracks use deterministic one-to-one nearest pairing. The model deliberately suppresses stationary edge decorations, but can also suppress stationary real enemies. Camera motion and parallax can make decorations move; therefore this does **not** establish reliable enemy identity.

The output is the existing local `shadow-edge-study.json` / `shadow-edge-study.md`, now extended with a `temporal` metric and per-frame confirmed/pending/stationary counts. Original baseline and raw shadow columns remain supported. No original trace, screenshot or label is modified. ARGUS does not integrate into WyrmForge, send input, or use its internals.

## Validation and promotion

The CI generates two synthetic PNGs with a moving colored sprite and a stationary lookalike, then executes the complete .NET CLI. Expected behavior on the labeled second screenshot: baseline 1 TP / 0 FP / 1 FN; raw shadow 2 TP / 1 FP / 0 FN; temporal shadow 2 TP / 0 FP / 0 FN. Synthetic success verifies plumbing, **not real-game precision**.

The real development set remains private and the pre-registered `run-01` held-out set must not be inspected during tuning. The temporal variants are intentionally not used in `VisualGameAgent`, `VisualCombatSteering` or `CombatPixelPerception.ObserveRgb`. Before promoting any such filter: measure its actual TP/FP/FN on separately captured human-reviewed development runs, assess misses from fast and stationary enemies, then conduct a fresh untouched held-out test and real-game safety trial.

**Next:** run the official CLI against privately preserved original `run-02` labels and investigate residual false positives/missed stationary enemies. If no .NET SDK is available locally, keep that validation pending rather than publishing private references in GitHub Actions.
