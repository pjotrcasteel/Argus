# ARGUS M2.13 — First genuinely trainable offline visual baseline

## What is new

This is our first **actual parameter-learning** component in ARGUS. A
deterministic class-weighted, L2-regularized **logistic regression** learns
its four feature weights, intercept, and feature normalization from
human-reviewed development-run pixel evidence. It is not a static
`if` rule, and it is not yet a model that plays WyrmForge.

The question is deliberately narrow: **when a shadow player memory
proposes a stale position, how much does current image-space evidence
support the player being visibly present there?** A target of `1`
means an independent human marked the player visible on the
original screenshot, and `0` means absent. This is not proof of
correct *actor identity*, threat recognition, strategic competence,
victory, or safe control. An unlabeled screenshot is **not** absence.

The input comes from M2.12's private output
`<development-run>/player-shadow-feature-rows.csv`:
`run_id`, screenshot step, shadow gap (first/second),
pixel-matched player palette count, local maximum RGB deviation,
high-contrast edge density, existing empty-patch result,
and optional externally human-reviewed label. The **existing veto status
is not used as an extra learned feature** because it is derived from
the same pixel measurements; it is only the fixed baseline for comparison.

Features (fixed before evaluation):
`log(1 + paletteCount)`, `rgbStddev / 64`,
`strongEdgeFraction * 4`, `secondGap ? 1 : 0`.
Each fold fits its **own training-run-only** mean and standard deviation.
No feature scaling, class weights, regularization or decision threshold
is fit with data from its held-out run.

The update loop does real (no external ML service) full-batch
gradient descent for **1500 fixed epochs** with **0.12** learning
rate, **0.04** L2 weight penalty, balanced per-class weights, and
a **0.5** decision threshold. No randomness, hyperparameter sweep,
subtle auto-threshold fitting, hidden credentials or downloaded
neural model is involved. Trained parameters appear in the report
**only if** training coverage passes.

## Hard "not ready" gate

Before fitting any weights, require:

- **At least 3 distinct** exclusively `dev-*` independent run identities.
- For EACH run: at least **10 genuinely human-reviewed shadow proposals**,
  at least **3 visibly-present** and **3 absent** references, and at least
  **2 absent-human examples the existing simple veto left indeterminate**
  (hard negatives).
- Finite valid current-image features, duplicate-free increasing frame steps,
  exact feature schema, honest labels `0`/`1`/`blank`, and no field
  contradicting the recorded original empty-patch witness.

Failure yields `status: NOT_READY`, human-readable blockers,
**no trained weights**, and **zero fold results**. Absence of
`player-shadow-feature-rows.csv` for a development run never makes
unlabeled samples magically usable.

The input reader enumerates **only immediate `dev-*` child directories**.
It never opens or traverses `run-01`, `run-02` or any other held-out
cohort and refuses run IDs that are not `dev-*`. A corresponding
synthetic regression test places deliberately broken held-out data
alongside development inputs and verifies they are not read.

## Leave-one-entire-run-out

On successful readiness, three or more models are trained separately;
for each fold, the **entire excluded run** contributes no data to
the fit, class balancing, normalization or threshold setting.
The generated JSON/Markdown reports per-run TP, FP, FN, TN, separately
from the original M2.11 empty-patch heuristic (which defaults
unresolved positions to optimistic `VISIBLE` and is NOT an identity
classifier). Run-level folds must not be reported as if individual
screenshots were independent procedural games.

A final set of research-only weights is also fit using all development
runs, **after** out-of-run scores are computed. It cannot be used
for ARGUS live gameplay. No code in `VisualGameAgent`,
`CombatPixelPerception`, navigation, action inputs or WyrmForge
depends on this package.

## How to use privately

First, on a trusted .NET 10 workstation, generate M2.12
`player-shadow-feature-rows.csv` beside each existing original
image/trace and genuinely human-reviewed `labels.json`:

```bash
dotnet run --project src/Argus.Evaluation.Tool -- --run PRIVATE_ROOT/dev-c --occlusion-veto examples/wyrmforge.profile.json
dotnet run --project src/Argus.Evaluation.Tool -- --run PRIVATE_ROOT/dev-d --occlusion-veto examples/wyrmforge.profile.json
dotnet run --project src/Argus.Evaluation.Tool -- --run PRIVATE_ROOT/dev-e --occlusion-veto examples/wyrmforge.profile.json

dotnet run --project src/Argus.Learning.Tool -- --runs PRIVATE_ROOT
```

The learning tool writes `offline-player-learning.json` and
`offline-player-learning.md` **locally**, outside public GitHub.
Do not upload real human labels, original development game PNGs
or learned private parameters to public CI or GitHub Pages.

The original manually reviewed dev-a/b/c/d/e labels **exist in private
conversation evidence** but a sufficient number of correct **human
absent on otherwise exploring bridge proposals** has not been
established. The actual private-run learning status is therefore
**unknown pending execution**; do not assert `RESEARCH_TRAINED`
or real-world performance just because the implementation compiles.

## What synthetic CI proves (and what it does not)

A reproducible fake **three-run** group contains 12 feature rows per
development run, of which 10 have artificial labels (5 visible and
5 absent, including 3 hard negatives) and 2 are unknown. This verifies
actual weight updates, finite parameters, run-held-out scores,
deterministic byte-for-byte repeatability and the ability to read
honest targets. A second **two-run** dataset must yield `NOT_READY`
and no model. A poisoned `run-01` file in the same root must stay
unread. MSTest covers malformed CSV, class imbalance, unknown labels,
duplicate steps, no held-out contamination and deterministic training.

Synthetic numbers prove **software implementation correctness** only,
not classification success in WyrmForge, generalization to new visuals,
safe player identity, or reinforcement learning. This is the first
supervised-learning building block, not a strategy-learning agent.

## Next scientific step

Run this entirely locally against independently marked original
development evidence. If `NOT_READY`, obtain a NEW opt-in,
precommitted visual reference sample emphasizing player-ABSENT but
still `exploring` cases; include varied textured and colored lookalikes,
without cherry-picking frames after seeing model predictions.
Only when enough independently reviewed development evidence exists
may a fitted model undergo a **fresh independent untouched
held-out cohort evaluation**. The old M2.4 `run-01` stays CLOSED.
Even passing such a test is not automatically authorization for live
steering; implement and verify a separate rollout safety gate.
