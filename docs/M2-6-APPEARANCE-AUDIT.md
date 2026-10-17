# ARGUS M2.6a — Visual appearance evidence, not a trained classifier

## Why

The independent **development-only** screenshot audits from M2.5c–e showed that simply extending red/blue/grey pixel palettes can find previously missed sprites but also mistakes particles, HUD and scenery for enemies. The moving-only shadow filter removed false positives at the price of missed stationary entities; blindly restoring persistent stationary candidates did not improve the six scored examples. The next step is to measure *what the candidates look like* before considering any classifier.

## Scope and API

`Argus.Agents.Visual.VisualAppearanceDescriptors` is a stateless, game-agnostic image feature extractor. It observes current screenshot RGB pixels only, for **candidate** rectangles produced by the existing unchanged baseline and shadow scanners. For each candidate it emits:

- Bounding-box center, width, height and whether the rectangle is clipped by the viewport edge.
- Mean luma, luma standard deviation and mean RGB saturation within the candidate rectangle.
- Absolute difference of mean luma between the candidate rectangle and an external four-pixel halo.
- Edge density (fraction of neighboring pixel pairs with luma jump ≥0.075 within the candidate).

Features are normalized to 0..1 (except the pixel coordinate and dimensions), not labeled or weighted. A textured HUD icon can look like a textured enemy. No threshold is inferred from six correlated development frames.

`Argus.Evaluation.AppearanceAudit` applies those descriptors to original PNGs for **pre-existing human-reviewed frames**. It never calls game internals. It calculates features first, then uses the shared one-to-one 26-pixel human-center matcher to attach *agreement with annotation*, not true enemy identity. Records are grouped by source (`baseline`/`shadow`) and `MATCHED_HUMAN`/`UNMATCHED`. Report aggregates descriptive group feature means and per-frame matched, unmatched and missed counts. Unknown human labels, noncombat screenshots, old stale final frames, and frames without an independently identified player remain unscored.

## Reproduce privately

Extract the original M2.4 development `run-02` screenshot/trace archive locally, put the previously recovered **real human** `labels.json` inside its `run-02` directory, and run:

```bash
dotnet run --project src/Argus.Evaluation.Tool -- --run results/research/run-02 --appearance-audit examples/wyrmforge.profile.json
```

Outputs are `appearance-audit.json` and `appearance-audit.md` in the **local** run folder; JSON contains numeric features and coordinates of predictions, not raw screenshot pixels or model weights. Nothing is uploaded. The untouched `run-01` held-out set must not be used for parameter selection or discovery.

## What this does NOT establish

There is **no trained ML model**, no enemy-vs-decoration classification threshold, no new navigation or avoidance logic, no AI self-learning and no win/loss inference. The original screenshot matching may differ from decisions obtained with an online temporal prior. A color-palette-generated candidate set also hides missed enemies for which no candidate exists: the report preserves per-frame unmatched human centers for this reason.

CI tests use deterministic synthetic RGB patches, and full PNG → baseline/shadow → feature extraction → manual-match audit → JSON/Markdown generation. Real WyrmForge smoke checks only that active play is unbroken, not whether measured descriptors separate enemies from decorative objects.

## Next research gate

Gather **at least two more independent development captures**, choose blind sample frames before reviewing detector results, and ask the owner to annotate only the genuine visible screenshots. Compare feature distributions and unmatched-human centers per *run*, not as thousands of independent frames. Then decide whether to train a small, reproducible, portable **offline** candidate scorer and evaluate it on a genuinely fresh untouched held-out session. Until then keep all feature extraction and experiments outside active player input.
