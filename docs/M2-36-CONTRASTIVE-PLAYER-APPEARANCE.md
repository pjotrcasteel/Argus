# M2.36 — trainable contrastive player-appearance metric (offline, not deployed)

## Why a new metric

The M2.34 original independently human-reviewed U/V/W experiments
conclusively **failed** all three whole-run-out development gates.
M2.35 introduced causal competing-object tracks, explicit static-HUD
abstention and genuine source continuity, but a coherent moving enemy can
still be mistaken for the player. ARGUS therefore needs **identity
evidence learned from example player appearance and difficult nonplayer
crops**, not a screen-center heuristic, fixed player-color rule, or
single motion novelty peak.

This new training algorithm is a **game-neutral exploratory prototype**.
It has NOT passed an unreviewed external game-data evaluation. Synthetic
toy image success is not player-recognition accuracy and does not establish
reinforcement learning or balancing ability.

## Frozen source-only learning procedure

`tools/m236/contrastive_identity.py` supplies:

1. Start with exact original reference *training* screenshots that have
   human-reviewed original-pixel player center if visible, otherwise
   an explicitly human-ABSENT full frame. Do not train on UNKNOWN, on
   held-out whole runs or on duplicated original PNG SHA across fold
   boundaries. Need >=8 real VISIBLE, >=2 genuine ABSENT and >=2 whole
   original development fitting runs. Provenance admission / whole-run
   split is performed separately by source-gate modules M2.34 / M2.32.
2. Use existing M2.30 game-neutral 288D 48px RGB/shape/edge
   descriptor, including x=0/x=389 original edge-repeat padding.
   Training uses human points ONLY to crop positive examples and
   EXCLUDE negative 64px-spaced background patches within 72px of
   a known actual player. Neither screen coordinates nor game-specific
   sprite/palette/asset IDs become descriptor inputs.
3. Mine hard-negative background/monster/UI crop features from the
   original **fitting** screenshots only: choose the nearest 25%
   of negative crops to the training-positive centroid in original
   feature-space; genuinely ABSENT screens contribute only negatives.
   No held-out/absent-calibration source pixels may choose negatives.
4. Compute a **trained diagonal, nonnegative appearance metric**:
   positive variance per feature versus the hard negative's
   distance from the human-positive centroid, with ridge .02 and
   capped contrast gain 10. Normalize feature weights to mean 1.
   Training retains only original fitting-player exemplars, centroid
   and fitted feature weights. This is not a pretrained neural net.
5. For an original screenshot location, score source-only shape
   features against the closest fitting-player exemplar and the
   centroid. Use **70% closest exemplar weighted squared distance
   + 30% centroid weighted squared distance**, negated as a logit.
   Score full 16px image grid, NMS 32px, keep max 12 local candidates.
6. The separately existing M2.35 causal tracker may consume these
   bounded source candidates using original predecessor/current image
   pixels, learned-weighted appearance and actual source SHA. Tracking
   from screen to screen is **not** the owner/identity ground truth.
7. For a future original evaluation, calibration must come from
   **>=4 distinct truly human-ABSENT entire screenshots** from
   **>=2 training source runs**, excluded from fitting/normalization
   and all held-run source SHA. Source-only highest logit must
   exceed the largest truly ABSENT calibration score + fixed .02
   to give a *candidate*. Two competing candidates within .04
   logit are ambiguous and must abstain.
8. Even above threshold, the return is strictly
   `UNVERIFIED_APPEARANCE_CANDIDATE`, never an actual confirmed
   player. `playerPresent: null`, `verifiedPlayerCenter: null`
   for every output; no online inference or game policy integration.
   No effective statistical error-bound or conformal probability
   guarantee can be claimed from 4 correlated absent frames.

## Scientific gates

This code is preregistered **before any new independent original source
cohort is evaluated**. Already inspected R/S/T and U/V/W are development
examples or diagnostics, NOT a prospective success benchmark for the
new metric. The fully artificial test suite checks learned separation
from static HUD / nonplayer shapes, source-only bounded candidate
generation, exact SHA/training vs absence calibration exclusions,
missing negatives refusal, deterministic coefficients, edge clipping,
and guaranteed abstention / UNVERIFIED distinctions. Real model weights,
owner labels, original preceding PNGs and private per-frame results stay
outside GitHub and public Actions/Pages.

**NEXT M2.37**: build a private full-run original screenshot replay
capable of combining M2.36 exemplar logits with the M2.35 temporal
hypotheses while respecting true-original chronological source
provenance, then independently capture fresh external-game
development source runs and obtain blind human VISIBLE-center/ABSENT/
UNKNOWN annotations. Once all features, fit/calibration partitions,
source SHA and evaluation thresholds are locked, require a
per-held-run preliminary gate of >=8 genuine visible, >=3 genuine
ABSENT, >=50% accepted centers within 35px and <=25% false
presence on ABSENT. A purely source-only unverified proposal
is **not** an accepted player detection; calibrated identity
acceptance must be explicitly designed before testing.

No O/P/Q prospective images or original M2.4 run-01 may be
opened, replayed, fitted or calibrated during development.
No changes to active ARGUS gameplay steering or WyrmForge code.
