# M2.16 — Offline image-local probe learning (first vertical slice)

## Goal and scope

M2.15 independently froze **36 original WyrmForge development screenshots / 72 location
questions** before human review. M2.16 introduces a **separate** offline learner for
those spatial locations. It is NOT a retraining of M2.13's rare temporal
`BRIDGE_ONE` / `BRIDGE_TWO` stale-player-memory samples. No temporal gaps or
player-presence labels may be fabricated to make the old model fit.

The trained parameters come from 5 numerical, game-neutral, **current PNG only**
features measured inside the fixed 53×53 square at each 26px probe:

- mean luminance
- luminance standard deviation
- contrast against a four-pixel surrounding ring
- strong gradient density
- mean saturation

The role (`center-anchor`/`spatial-control`), x/y, sample ID, frame index, game,
human label, player prediction and the location-based baseline are **excluded**
from model features. A frozen fixed-step, L2-regularized, class-balanced logistic
classifier learns weights rather than hardcoding them. For each development-run
fold, means, deviations and coefficients come only from the other two complete
runs. A *center-anchor-is-positive* baseline is reported separately to expose
study sampling/location bias, rather than pretending the fixed center versus
peripheral grid is representative of arbitrary candidate locations.

## Original image / private human export integrity

The `--spatial-archive` CLI takes ONLY the original 41-file M2.15 authentic
**review bundle**, containing 36 original PNGs, exactly three blank reviewer
HTML pages, homepage and frozen study manifest. The separate `--labels-f/g/h`
are supplied from private owner exports, never in a public repo/workflow.

The admission gate checks frozen M2.14/M2.15 seeds, distinct independent
development run identities, byte-level SHA256 for every original image,
390×844 viewport, 24 immutable probes per run, exact ID/coordinates/radius,
12 center + 12 spatial roles, all-UNKNOWN source reference, strictly enumerated
ZIP contents, no extra label fields or duplicate/replaced samples.
Human `unknown` is never converted to negative.

**Contradictory positive labels at disjoint locations in the same screenshot
block the study**: the one player cannot occupy both disjoint 26px circles.
The importer explains which frame needs human re-review, and never silently
corrects or guesses the label. No model is created after such a rejection.

The gate additionally requires each independent run to have all 24 original
reference questions, at least 10 actually reviewed and at least 3 **present**
and 3 **absent**. Failing sufficiency returns `NOT_READY`, **no scores or
weights**. Valid sufficient development-only data returns
`EXPLORATORY_TRAINED`; this must not be confused with proven generalization.

## Private execution (never run with real labels on public GitHub Actions)

```bash
dotnet run --project src/Argus.Learning.Tool -- \
  --spatial-archive PRIVATE_M215_REVIEW.zip \
  --labels-f PRIVATE/dev-f.json --labels-g PRIVATE/dev-g.json \
  --labels-h PRIVATE/dev-h.json --output PRIVATE/learning
```

This produces private `spatial-probe-learning.json` and
`spatial-probe-learning.md`. Never commit either, upload private human JSON
or private models to Actions/Pages, or derive test fixtures from those labels.
The old `--runs` M2.13 CLI is unchanged.

## Validation and limitations

Deterministic MSTest with **artificial PNGs and artificial labels only**
exercises original SHA256 verification, source/annotation validation,
impossible-human-reference veto, unknown coverage, no held-out ID acceptance,
data/score determinism, and leave-one-**whole-run**-out training.
The actual original M2.15 workflow and Pages publication are unaffected.

The three original runs are DEVELOPMENT sessions selected by the same
frozen protocol, so leave-one-run-out is useful exploratory comparison but
**not a prospective independent test**. The center-biased sample protocol and
only three runs cannot establish reliable recognition outside this narrow
study, regardless of the fold scores. Repeated neighboring screenshots are
correlated; do not treat 72 local probes as 72 independent games.

No models are consumed by the runtime agent. **Do not** open or tune against
M2.4 `run-01`, publish owner labels, change live steering, claim a learned
strategy, or equate the existing live smoke gate with gameplay success.

## Follow-on experiment

After owner re-review of any contradictory private sample, run this M2.16
research study locally and inspect each run's confusion matrix **against
the center-only baseline**. If the model cannot outperform positional sampling
on out-of-run development sessions, next collect more varied independent
positions with preregistration. Even positive results require an entirely
**new never-trained-on** run cohort and a separate safety rollout before
live ARGUS policy integration.
