# M2.35 — causal player-identity hypotheses with negative controls (shadow only)

## Motivation and evidence

The independent owner-reviewed original U/V/W development audit at M2.34
[NO-GO](M2-34-ORIGINAL-UVW-OWNER-FROZEN-FAILURE.md) for all three held-out
runs, despite genuine human-ABSENT calibration images. It is not scientific to
call a top patch "the player" simply because its shape looks appropriate, nor
to lower the trained M2.32 absence cutoff until those already-seen frames
produce attractive metrics. Distinct lookalike enemies, HUD, transient FX,
edge-clipped sprites and genuine player ABSENCE remain unresolved.

The next hypothesis is **temporal visual object identity** rather than a
single-frame saliency ranking: a candidate must exhibit consistent local
appearance over a sequence of truly consecutive original screenshot frames,
with a bounded position trajectory and actual pixel changes. Coherent
objects might nevertheless be **enemies, particles or animations**.
It is not a verified player detector.

## Implemented source-only vertical slice

`tools/m235/causal_identity.py`, applied ONLY in offline research:

- Sample full original 390×844 RGB pixels on the existing 16px image-wide
  grid including x=0, x=389, y=0, y=843; 48px edge-repeated
  appearance descriptor from the **fit-only** model. No x/y player anchors
  enter model features, and no WyrmForge code, hidden state, DOM, asset,
  entity IDs, engine actions or model predictions are ground truth.
- Rank top 12 learnt local image patch proposals and apply 32px image
  non-maximum suppression. Keep arbitrary visual lookalikes competing;
  no privileged knowledge about which is player.
- For each candidate compare the 48px original patch with the same
  screen coordinate in the **immediately preceding original screenshot**,
  measured as mean absolute raw RGB change divided by 255. Source
  pixels and 64-character SHA-256 are mandatory. This deliberately
  treats identical static HUD graphics as untrustworthy BUT also must
  abstain on genuinely stationary players with no changing pixels.
  It is not a verified optical flow or camera-motion model.
- Causal one-to-one greedy association only on **consecutive** original
  steps (no interpolated or selection-gap tracking), with a maximum
  120px per-step displacement, at least 0.86 cosine of the local shape
  descriptor, and joint score based on appearance+displacement.
  Greedy matching is not guaranteed to recover the true object and
  can switch identities when creatures overlap.
- A track is only *eligible* for an unverified identity hypothesis
  after 3 consecutive authentic source frames and 2 actual source
  changes above 0.012. Equally supported matured competing hypotheses
  whose raw learned logits differ by less than 0.40 **ABSTAIN** rather
  than claiming a unique player. These are experimental fixed constants,
  frozen prior to future independent cohort annotation.
- Whole-run change, missing frame step, repeated current original SHA,
  mismatched immediately preceding original image SHA or broken
  chronological identity **resets temporal evidence**; never bridge a
  failed/missing original pair. A malformed or out-of-order source
  frame causes a hard error.
- Every response carries `playerPresent: null` and
  `verifiedPlayerCenter: null`; a tentative `candidate` is separate
  and explicitly `UNVERIFIED_SOURCE_ONLY_IDENTITY_HYPOTHESIS`, never a
  verified detected mage. `liveAgentChanged: false`.
- 18 **entirely artificial** tests cover causal persistence, unchanged
  HUD and stationary player abstention, competing lookalikes, wrong
  association, source byte identity, source run reset, temporal holes,
  repeated image SHA, out-of-order steps, edge x=0/389, genuine RGB
  frame-wide source sampling, and lack of player oracle.

**Important:** all historical U/V/W and R/S/T are now **seen**
development/diagnostic data. Synthetic tests demonstrate interface
semantics, not game competence, and any inspection of old labels cannot be
presented as prospective validation of M2.35.

## Scientific readiness and next research gates

M2.35 is an explicit **research architecture and causal hypothesis**, not
player learning validated on a real independent cohort. It does not
replace the old M2.32 model, does not generate new genuine player
ground truth, and is not integrated into live Argus or WyrmForge.

The next substantive stage **M2.36** is to define an independently
replayable private real-browser screenshot provenance and one-shot
model scorer, then capture **three NEW external whole-game development
sessions**, with browser original images rather than invented context,
timestamp/trace proof and original-byte SHA-frozen unbiased original
chronological source target selection. Independent owner-blind
point/ABSENT/UNKNOWN annotations must be collected **after** all features,
learned model, calibration and thresholds are locked.

Before any truly independent development evaluation, require whole-run
train/validation source disjointness (including exact repeated image SHA),
independent genuine human-ABSENT whole-frame controls, and a meaningful
identity-versus-absence gate (at least 8 genuinely visible and 3 genuinely
absent per test run, >=50% *accepted* player centers within 35px and <=25%
false presence on human ABSENT, report each whole run separately).
A shadow candidate without verified identity does **not** pass that gate;
a null/unknown abstention is not a correctly detected player.
Introduce explicit learned/calibrated player-vs-other scoring before
claims of true success.

The original 54-frame M2.23 O/P/Q prospective source ZIP remains SHA
SEALED, never extracted/tested/tuned here. M2.4 original `run-01` also
remains unopened. Live game input policy and current runtime untouched.
