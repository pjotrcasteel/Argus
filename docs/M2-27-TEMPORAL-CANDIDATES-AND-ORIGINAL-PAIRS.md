# M2.27 — Causal visual candidate tracks and fresh original screenshot pairs

## Why M2.27 exists

M2.26 fixed the useless all-ABSENT classifier but found the **wrong
highest-scoring visual region**: only 3/36 actual player locations
correct, four false presence calls across 18 truly empty screens.
The previous score is a **failed development diagnostic**, not evidence
of a player detector or improved autonomous game playing.

The M2.27 source-only module at `tools/m227/temporal_candidates.py`
does **not** output a player coordinate, player presence, movement
command, action, model weight, or a player class. It returns hypotheses
that MAY include terrain, spells and HUD. There is no WyrmForge source
coupling, sprite, color key, DOM, OCR player location or privileged
game state. Live ARGUS remains **unchanged**.

## Frozen temporal hypothesis protocol

1. Work on two original successive (not synthetic/interpolated)
   **390×844 RGB screenshots** in causal order. Do not use screenshots
   from the future. Underpowered/missing registration => zero novelty
   and explicit abstention, not invented player positions.
2. Convert whole screenshots to luminance, downsample by four, extract
   edge magnitudes, use Hann-windowed FFT phase correlation to estimate
   global scene translation. Search only ±80 original pixels. A
   peak/independent-neighbor contrast below **1.08** is unreliable.
   This is a camera/scene alignment proposal, not a recovered actual
   engine camera coordinate.
3. Shift the old image without periodic wraparound and compute
   `max(|current - aligned previous| - |current - screen-fixed previous|, 0)`
   for valid pixels when movement totals at least four pixels. The
   resulting quantity is **foreground-like novelty**, not player
   probability. A stationary player, a HUD health widget, menus,
   overlays and other screen-anchored features can all score high.
4. Freeze a spatial grid of 16 pixels, candidate-centered local
   24×24 novelty mean, minimum 0.03, 32px radius greedy
   nonmaximum suppression, 16 total proposals; proposals carry
   current-pixel center, original local score and a **67-value**
   8×8 grayscale-shape + RGB-mean signature from an unaltered
   local 40×40 screenshot crop (edge padded).
5. Causal track association between earlier and later candidate
   observations uses cosine appearance similarity ≥0.65,
   movement ≤110 screen pixels, and deterministic greedy pairing.
   Tracks with ages are **hypotheses**; both real characters AND
   long-lived HUDs may form convincing tracks. No hidden/true player
   identity is inferred. Offscreen, uncertain or stationary
   contexts may yield zero tracks. The algorithm's current greedy
   ordering may cause ID switches; this is not robust multi-object
   tracking.
6. Tests MUST include a scene with a screen-fixed fictional character
   AND a fixed HUD while the background pans. **The HUD is
   allowed to rank higher**; that negative test demonstrates motion
   does not solve identity by itself. No synthetic test can prove
   genuine player detection.

These fixed choices are authored before capturing any new R/S/T
external gameplay or asking for new player labels. Do not adjust
parameters after evaluating any prospective sources or claim
independent accuracy from previously reviewed I/J/K/L/M/N.

## New independent original screenshot sources

`tools/m227/freeze_temporal_context.py` preregisters exactly three
fresh whole-browser `dev-r`, `dev-s`, `dev-t` development
sessions with seed
`argus-m227-temporal-context-rst-20261018-v1`. Selection:
18 SHA-seeded chronological bins per independent original session,
target original step ≥1, plus **immediately preceding original
step-1 screenshot** for motion context. This is **not** a
synthetic previous image and it never uses agent guesses, human
labels, visual phase classification, success estimates or run
outcomes to select frames.

Each R/S/T private research artifact contains:
- 18 original selected 390×844 PNGs in `annoteren/dev-{r,s,t}/`;
- the exactly corresponding 18 predecessor PNGs in
  `context/dev-{r,s,t}/`;
- one SHA/original-step/pair manifest with source trace digests;
- one **BLANK** mobile point-review HTML per development run,
  original-pixel taps/ABSENT/UNKNOWN and local JSON export/import;
- no owner truth, game source, hidden state or prior prospective
  `dev-o/p/q` images.

The full original capture trace stays in a separate **private**
temporary Actions artifact. Only ordinary visual player controls and
screen captures are used. Real game outcome, actual player recognition
and version are **UNKNOWN**, even if Chrome/UI smoke tests pass.
The source artifacts expire after seven days; retain original
backups when generated.

Owner standing permission covers public **development screenshots
and BLANK reviewers**, but NOT raw traces/context provenance,
label exports, model weights or sealed prospectives. Public Pages
publication must create a separate SHA-pinned static checkpoint
including **only the 54 TARGET development PNGs and 3 BLANK reviewer
HTML pages**, not the 54 predecessor context images or raw source
traces. Existing historical pages must remain byte-identical.

## Scientific gate for the next research step

This M2.27 slice tests a **source-only candidate generator**, not a
player tracker. After **independent, exact original SHA-matched**
human R/S/T labels, report these separate measures:

- True-visible fraction with at least one of the **top 16 image
  proposals** within 35px of human player center;
- distribution/rank of the closest true-player proposal and how
  often HUD/non-player gets higher novelty;
- number and persistence of source-only proposals on **whole-screen
  human-ABSENT** frames (false candidate burden, not player false
  positive rate);
- reliable registration, abstention, and temporal track-ID continuity
  across successive original game frames, never assuming a persistent
  UI element is a player;
- against existing image-only fixed-center baseline, without
  retrospectively tuning on labelled left-out runs.

Minimum **investigate-further** proposal criterion: >=70% of visible
frames in **each new independent R/S/T run** have a candidate within
35px among the frozen 16, AND fewer than 30% of confirmed wholly
ABSENT frames have any source-only proposal. This aggressive latter
criterion may be impossible given static HUD overlays; if so report
NO-GO, not a manually filtered success. This is not a trained model
go-live threshold and cannot justify runtime integration.

**Never open sealed O/P/Q** or original M2.4 run-01 until a
separately trained, calibrated, frozen model with exact coefficients,
source hashes, parameters and evaluation protocol is locked for an
independent one-shot assessment. Previous L/M/N is already seen and
not independent prospective evidence.
