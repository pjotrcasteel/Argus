# M2.29 — original R/S/T player-reference audit (offline, owner-private)

## Purpose

The owner's three genuine original-image reviews (dev-r/s/t, 18 selected images
each) permit the first source-verified audit of the **frozen M2.27**
screenshot-only *proposals*. This is **not** a detected player identity, agent
improvement or game outcome, and cannot be used to enable live steering.

M2.27 predeclared a strict **investigate-further** gate per whole run: at least
70% of human-visible player centers within 35 original pixels of at least one
of its top 16 candidates, AND fewer than 30% of entirely human-ABSENT screens
with any candidate. Each run must pass; never pool runs to conceal a failure.
A candidate on an absent image is a **candidate burden**, not a deployed-model
false positive. No identity-specific accuracy can be inferred from the
stationary center baseline either (it always claims a player on absent screens).

## Immutable source / private-reference boundary

- Exact original 113-file development ZIP: GitHub Actions artifact
  `12137923805`, SHA256
  `bdb8538c90ccb5166fcee4b94e9f46e6c4ba1a91d70079c370c4c13d65173bda`.
  This artifact expires October 25, 2026; preserve a **private offline backup**.
- Public Git snapshot `public-annotator-site-m227-development` contains 54
  original selected target images ONLY. The 54 original immediately preceding
  images are privately preserved in the authentic source ZIP; never publish
  them, owner reviewed locations, annotation JSON, original source traces,
  learned coefficients or per-image diagnostics.
- The three separate owner-reviewed exports are named
  `m227-dev-{r,s,t}-player-points.json` and must be kept outside the repository.
- Source ZIP hash, CRC, full allowlisted membership, original 390x844 PNG
  hashes, exact prior-step hashes, strict review schema/seed/partition/run IDs,
  chronological frame IDs and original SHA matches all fail closed. Identical
  original screenshot hashes with contradictory human labels are rejected.
  UNKNOWN is never converted into a guessed present or absent.
- The entire pair is processed with the **unchanged** M2.27
  `analyze_pair(previous, current, step)`. Labels are only used **after**
  generating proposals. Frame-gap tracks are explicitly reset: sparse chosen
  still images do not imply continuous observed tracking.
- Only a single private JSON report is produced. Optional frame details are
  private and never part of CI. The public test suite uses exclusively
  artificial RGB PNGs and synthetic human labels, never real owner data.

## Exact private command

With the authentic source ZIP and owner's three exports in a local non-Git
private folder:

```bash
python -m pip install "numpy>=2,<3" "Pillow>=11,<13"
PYTHONPATH=. python tools/m229/evaluate_temporal_references.py \
  --original-archive /private/argus_m227_original_pairs_private.zip \
  --labels-dir /private/argus-owner-reviews \
  --output /private/m229-report.json
```

Do not give GitHub Actions, Pages, Issues, public PRs or live ARGUS access to
`/private`. The CLI enforces the exact archived SHA with no production override.
Tests can inject a SHA only through the `evaluate` function with synthetic
fixtures; this never changes the production CLI.

## Development outcome and decisions

- The owner supplied complete reviews for all 54 selected R/S/T targets on
  October 18, 2026. Their selected original SHA IDs and all private pair
  hashes were independently checked; exact private run-level metrics were
  computed locally, NOT uploaded to this repository. The previously frozen
  gate is **NO-GO** on each development run.
- Some images have a repeated authentic SHA across adjacent distinct frame
  steps; they are observations but not independent visual examples. The
  evaluator records distinct image SHA count and per-run denominators.
- Registration confidence is NOT candidate coverage. Scene-stationary / weak
  or zero relative movement may produce zero candidates despite human-visible
  players; prominent stationary UI, HUD or peripheral structure can outrank
  player hypotheses whenever candidates are present. These mechanisms need
  causal diagnosis, not retrospective gate editing.
- Existing live combat steering / player observations and all active agent
  policies remain unchanged. Neither prior real-human scores nor synthetic
  tests establish game balance, win rates or trainable player-recognition
  performance.

## Next M2.30 research priority

Preregister a game-neutral visual identity learning design trained solely
on new and already admitted **development** human references. Distinguish
presence / location / abstention, person-like local shape and learned
appearance from untrusted camera saliency. Explicitly account for left/right
edge, HUD, identical repeated PNGs, unseen whole-run generalization and
ABSENT negatives. Group by **entire source run** and exact original SHA.

A detector and rejection calibration must be locked (feature schema, training
split, normalization, coefficient/hash, threshold, no-go) **before** touching
any genuinely untouched prospective original O/P/Q image. Those remain
sealed. M2.4 run-01 also remains unopened. Do not activate live steering,
and do not use recent owner development labels as if they were held-out
validation. Live Spectator mode is separately tracked in issue #11.
