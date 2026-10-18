# ARGUS M2.32 — preregistered independent U/V/W development and image-relative presence

**Stage at authoring:** all design, selection rules, absence handling, fitting/held-run
splits, and acceptance limits below are specified **before the NEW U/V/W game
sessions or their human annotations are captured**. Previous R/S/T analyses
were seen and used only as an explicit failure diagnosis; they are NOT
independent prospective evaluation evidence for this proposal.

## Aim

M2.27's motion proposals covered only 2/42 actual visible player centers
within 35px in its original R/S/T development audit; the M2.30 fitted local
shape model improved that count but failed all three development-fold gates.
M2.31 proved that low accepted location coverage hides two distinct problems:
the highest-scoring *unaccepted* local patch is often near the real mage, while
other frames rank HUD/monsters more strongly and some ABSENT frames are falsely
positive. Lowering the old threshold on reviewed R/S/T would be overfitting.

M2.32 tests an alternative **hypothesis**: learned appearance evidence should
be compared against background and separate competing objects in the same
image, *before* rejecting a whole frame based on independent, actually
human-ABSENT screenshot controls. Even on synthetics this cannot establish
real player identity or guarantee calibrated uncertainty.

## New hypothesis frozen in code

`tools/m232/relative_presence.py`, constants locked before the first
U/V/W real capture:

- Candidate image logits must come from a learnable **game-neutral local
  appearance model** (initial research baseline: M2.30 fit on the new
  development partition ONLY); neither pixel coordinates nor game state,
  WyrmForge asset classes/palette, DOM, OCR oracle, old live detector
  output or temporal proposals are features.
- Scan the complete **original 390×844 RGB** image including left/right
  edges, same fixed 16px grid as the M2.30 local appearance baseline.
- For all image candidate logits (z), top winner (z_1), and highest
  candidate (z_2) at least 64 original pixels from the winner, compute:
  `spread = max(0.25, Q75(z) - Q25(z))`
  `backgroundContrast = (z1 - Q90(z)) / spread`
  `objectDominance = (z1 - z2) / spread`
  `imageEvidence = min(backgroundContrast, objectDominance)`.
  The best local coordinate is only a **hypothesis**, never automatically
  player identity. Competing humanoid enemies or HUD elements may make
  evidence zero and cause correct abstention OR false negatives.
- Whole-image absence threshold: the **maximum** imageEvidence of at least
  **four distinct exact-SHA human-ABSENT whole screenshots** drawn from
  **at least two other development runs**, excluded from fitting and feature
  normalization, plus a frozen additive `0.02`. Reject scores
  *equal* to that threshold. This conservative score has NO statistical
  conformal/p-value or i.i.d. guarantee; adjacent images are correlated.
- Strict whole-source-run-out: hold each of dev-u/v/w completely aside.
  Exclude every original PNG SHA in the test run from ALL fitting and
  calibration runs. Deduplicate other original PNG hashes before division.
  Per training run, ordered true human-ABSENT unique screenshots are split
  alternately: indices 0,2,... to calibration; the remainder to fit.
  Every actual UNKNOWN screenshot is excluded from both. Insist on at least
  eight fit visible examples, two fit truly-ABSENT images, four calibration
  images from two runs, otherwise `NOT_READY`.
- Define the **development investigate-further gate** ahead of time:
  per held-out entire new source run, at least 8 original human-VISIBLE
  examples and at least 3 human-ABSENT examples; >=50% of visible
  frames accepted with a center within 35 original pixels and <=25% of
  truly absent frames falsely accepted. No post hoc aggregation makes
  a failed run green. Compare to an always-center/always-present baseline
  and the frozen M2.30 model as explicitly previously seen diagnostics.
  `INVESTIGATE_FURTHER` is NOT permission for live deployment. The
  original O/P/Q sealed one-shot gate remains independently locked.

## Fresh genuine external-browser source protocol

Only `dev-u`, `dev-v`, `dev-w`; `argus-m232-independent-uvw-development-20261018-v1`.
No M2.23 prospective run, old run-01 or human label enters new capture.
Existing unchanged .NET 10 ARGUS player is run against public external
WyrmForge using ordinary Playwright Chromium screenshots + mouse/keyboard.

- Three independent external browser processes; viewport 390×844;
  max 76 steps and 190-second bounded process; honest exit code, actual
  `summary.meetsVisualCombatGate == True`, 36–180 chronological original
  PNGs, exact trace SHA256, independent trace digests, 390×844 PNG geometry,
  and original policy/profile identity metadata all mandatory.
- Failed real runs may retry **up to three** genuinely new browser processes,
  never convert an unsuccessful code/visual gate to success, never fabricate
  scene/players. Persist the exact *successful attempt's* actual trace.
- Without looking at player estimates/actions/outcomes or labels, select
  **one screenshot in each of 18 chronological bins per whole run**
  using SHA256(seed + run + bin index). Selected step >=1;
  retain that exact genuine original screenshot plus its immediately
  previous original frame as private movement context, strictly SHA-pinned.
- Exactly 54 original **target** PNGs + 54 original **preceding** PNGs,
  3 entirely blank standalone mobile annotator HTML pages, one source
  manifest and homepage; 113 source output files, no filled JSON, private
  owner answer, train weight, trace JSONL, sealed prospective or old test
  snapshot in the blind reviewer package.
- Both authentic original raw whole-run captures and frozen annotated-image
  **blank** package remain 7-day temporary workflow artifacts initially.
  Do not silently publish screenshots or predecessor contexts to Pages.
  Standing approval for public original **development target screenshots
  and blank pages ONLY** does not authorize publishing predecessor images,
  user labels/coordinates, raw traces, weights or sealed prospective
  pixels. A separate SHA-pinned, reviewed Pages-only deployment may be
  created after actual source artifacts pass and provenance is frozen.

## Verification and no-guesstimate controls

The M2.32 source and pure score modules have no user label coordinates or
hardcoded target game's player color. CI runs only **artificial** source
PNG captures, corrupted provenance/duplicate rejection, numeric contrast
and zero-evidence abstention, run-disjoint split, and fully artificial
mobile Chromium 320/375/430px source-frame touch/export/import/reload.
When the real capture workflow finishes, report whether it was
SUCCESS or FAIL honestly; a green synthetic PR is not evidence of new
real source capture. A real capture remains **UNLABELED** until the owner
independently submits authentic private original-frame human reviews.

After original U/V/W owner annotations, score this preregistered
development trial ONCE, report any `NOT_READY`/failure per run and preserve
owner exports, original screenshot context, fitted model, and per-image
metrics outside GitHub. Do **not** tune in response to review outcomes and
rebrand the tuned result as a new independent test. Keep sealed O/P/Q
54-frame prospective original backup privately preserved (original
Actions artifact expires 2026-10-25) and M2.4 `run-01` unopened.

No active ARGUS game policy or player-identity detection changes here.
