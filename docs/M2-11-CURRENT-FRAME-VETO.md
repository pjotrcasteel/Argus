# M2.11 — Current-frame negative visual evidence for stale-player shadow memory

## Scope and motivation

M2.10 demonstrated a **research-only** last-seen-player bridge for up to two missing
RGB observations. The old, deterministic synthetic reference contained **two
correctly near human-visible actor centers** and **two distinctly wrong stale
proposals over human-confirmed ABSENT actor frames**. Keeping an earlier pixel
location cannot establish whether a player remains in that region.

The real owner-reviewed M2.7 development images showed an actor still *partially
visible* despite the existing player color component being rejected: `dev-d`
step 18 has an effect-merging wide blue shape, while `dev-e` step 39 has small
blue fragments. An independent local RGB patch inspection found approximately
**484** and **72** player-palette pixels respectively in 49x49 neighborhoods.
These are development-only descriptive figures, not official .NET quality
measurements; original annotated screenshot bytes and coordinates remain
private. `run-01`, the original held-out cohort, was **not opened**.

A naive rule suppressing all low-blue-pixel positions could incorrectly reject
the human-visible occluded actor; merely finding colorful pixels would
mistake decorations/particles for proof of a human player. Instead this
slice implements **only a conservative negative cue**.

## The rule

`PlayerCurrentFrameWitnessProbe.InspectRgb` reads current-frame RGB pixels
inside a fixed **49x49** region centered around M2.10's
**last genuinely pixel-detected actor**. Nothing in the source image
requires a WyrmForge-specific DOM, engine state, sprite atlas or hidden
game telemetry. The sampled region is clamped at screen edges.

It reports:
- current player-palette pixel count, using the **existing configured color rule**;
- maximum per-channel RGB standard deviation in the current patch;
- fraction of neighboring pixel edges with at least 15 units of
  single-channel contrast;
- a **binary negative cue only**.

`EMPTY_PATCH_VETO` requires simultaneously **zero** local player-palette
pixels, maximum channel deviation **under 5**, strong-edge fraction
**under 1%**, and at least 400 current pixels sampled. These conservative
heuristic cutoffs are **not calibrated probabilities** or universal
proof of player absence. If *any* condition does not hold, report
`INDETERMINATE`, **never `PLAYER_FOUND`**. A colorful decoration,
spell particle cloud, repetitive textured background, or solid
blue-painted decoy may be undecidable. It can still produce an incorrect
stale memory proposal and must not be steered toward.

No value from this probe is used as `previousPlayer` in the original
`CombatPixelPerception` or in `VisualGameAgent`.
The M2.10 two-frame memory, its temporal reset rules, game movement and
candidate enemy detection are untouched.

## Offline original-screenshot study

`PlayerCurrentFrameVetoStudy` **first** computes all M2.10 raw causal
shadow hypotheses from original screenshot PNGs in chronological order.
It then independently reads **only the current PNG for each raw bridge
proposal** and measures the 49x49 stale-centered region. Human references
are used **only for post-hoc scoring**, never to choose current regions,
retrofit earlier observations, create model labels, or alter the current
pixel evidence.

Its output `player-current-frame-veto.json/md` counts explicitly:
- total raw M2.10 bridge proposals and separately vetoed/unresolved proposals;
- **vetoed over human-visible player** (false vetoes; critical harm);
- vetoed over human-confirmed ABSENT player;
- **unresolved over human-confirmed ABSENT player** (residual dangerous proposals);
- unresolved over visible player; unknown and entirely unreviewed separately.

Existing original recorded frames, labels, pixel segmentation, route
selection, movement and live inputs are **not** changed. The new CLI is
opt-in and works entirely on a private development run:

```bash
dotnet run --project src/Argus.Evaluation.Tool -- \
  --run PRIVATE_M27_ROOT/dev-d --occlusion-veto examples/wyrmforge.profile.json

dotnet run --project src/Argus.Evaluation.Tool -- \
  --run PRIVATE_M27_ROOT/dev-e --occlusion-veto examples/wyrmforge.profile.json
```

The assistant runtime has no accessible local .NET 10 SDK, so these
**private original-image .NET results are not yet verified**; do not
upload the owner-provided real labels to public CI. The original M2.4
held-out dataset must remain unopened for tuning.

## Strict falsification tests

Six MSTests validate a homogeneous dark background veto, a tiny
still-human-visible blue fragment, an overwide colored magical effect,
checkerboard red decoration with rich texture but no player-colored
pixels, a uniformly blue-painted decoy, and screen-edge/invalid
coordinate handling.

A full synthetic **11-frame actual PNG → existing M2.10 pixel
tracker → new current-image veto → private-reference report** CLI
test requires **two false M2.10 bridges to be rejected** over
homogeneous empty images and **two ambiguous visible-player
occlusions to remain undecidable**. That is NOT verified gameplay
improvement, detection precision, actual human-absent reliability,
or evidence that a classifier exists. With textured empty scenery,
a false bridge may remain `INDETERMINATE`: this negative-only cue
is insufficient to promote stale guesses to game actions.

The standard bounded WyrmForge screenshot-only smoke must pass
unchanged. Merge only after full .NET10 CI and real-browser gates
are successful.

## Subsequent question

Evaluate this rule using original *private* development screenshots
and additional manually reviewed **player-absent, textured** frames;
report how often it fails to veto genuine false memories and how
often it rejects genuine occlusions. Only after that can an
independently designed, stronger current-frame visual discriminant
be proposed, trained only on development runs and frozen before
an altogether new untouched held-out evaluation. Do not adapt this
threshold to `run-01` or claim safe live navigation.
