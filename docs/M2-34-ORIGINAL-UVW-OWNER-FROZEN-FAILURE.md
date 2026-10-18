# M2.34 — exact original U/V/W private player-reference admission and frozen M2.32 evaluation

## Scope, privacy and outcome

The owner independently supplied three authentic 18-screenshot `m232-dev-{u,v,w}-player-points.json`
exports after the original-only mobile review production release. These are
**private owner evidence**, not repository contents. Their original selected
development screenshots and their immediate original predecessors were independently
source-SHA-matched, and the **previously frozen M2.32 appearance-vs-presence
hypothesis was evaluated once without changing the training procedure,
features, score formula, absence calibration or per-run go gate**.

**Outcome: NO-GO on each of the three independent U/V/W DEVELOPMENT runs.**
The model remained too conservative and frequently abstained even when a human
clearly marked a visible player. The player-ABSENT error rate alone cannot
justify promotion to a live player detector. Both the M2.30 baseline and this
preregistered M2.32 development research lack adequate real player
localization. No current live ARGUS/game actions, policy, model or WyrmForge
code were changed.

Exact private original player coordinates, PNGs, context pairs, individual
predictions, per-run private numerical metrics, learned coefficients and
locally fitted threshold hashes **are NEVER committed to GitHub, Actions,
Issues, Pages or PRs**. The owner receives full private results separately.

## Mandatory provenance

- Exactly 113-member authentic **development-only** original U/V/W review
  source from M2.32 GitHub Actions artifact `12142830180`:
  `ab1c338aea8af582b0c93de2f7c7b4f11528b62c50cf03ce1fd3e013dff7e26f`
  SHA256. Original genuine independent browser sessions U/V/W source
  images, not hallucinated/synthesized/interpolated PNGs.
- Frozen study seed:
  `argus-m232-independent-uvw-development-20261018-v1`.
- Per original run 18 selected original 390×844 target screenshots
  plus 18 **authentic immediate predecessors**. Every target SHA and
  preceding SHA is checked against the frozen source manifest, with
  chronological original step, exact selected target filename,
  owner review SHA and review original run identity.
- Exactly three complete human annotation exports, all 18 per original
  run. Validate schema version, frozen seed, independent source run ID,
  DEVELOPMENT partition, visible-center original-pixel bounds,
  true ABSENT with null center, and UNKNOWN with null center.
  No invented inference from confidence or an unreviewed screenshot.
- One original image SHA appears twice across different runs. Original
  SHA identity is maintained and all reference conflicts are refused.
  M2.32's **previously committed whole-run-out** algorithm excludes
  any matching test-run SHA from training **and** absence calibration.
- Source ZIP rejects unknown members (especially sealed O/P/Q,
  old run-01, source trace JSONL, weights, or owner labels), duplicates,
  path traversal and corrupt CRC.

## Frozen original-U/V/W research protocol

`tools/m234/evaluate_private_uvw.py` reuses the already merged
`tools/m230/identity_study.py` (class-balanced linear 288D appearance
fit, 16px original pixel grid) and **unchanged**
`tools/m232/relative_presence.py` (original image Q90 contrast,
best distant competing object, fixed source-ABSENT margin).

For each left-out whole source run:
- Train only on the other whole-development-run verified VISIBLE
  and eligible ABSENT originals, with exact SHA deduplication.
- Choose truly ABSENT original whole screenshots for calibration
  on the previously frozen alternating-in-time rule, disjoint from
  learned fit pixels and normalization; require **at least four**
  different whole ABSENT original screenshot SHA from **both** other
  training runs.
- Score full original 390×844 screenshot including both screen edges.
  The top source score is only a candidate. Presence is accepted only
  when its image-relative score **strictly exceeds** independently
  calibrated maximum of genuine ABSENT source-screen scores plus 0.02.
- Report visible within-35px true-player center, true-ABSENT
  false-presence burden and UNKNOWN separately, with an
  always-center/always-present honesty baseline. Keep raw-best player
  candidate coverage **distinct** from accepted player recognition.
- The previously frozen developmental investigate-further gate was
  >=8 true-visible and >=3 truly ABSENT source-originals PER held-out
  run, >=50% correct accepted visible location and <=25% false presence
  on ABSENT. Lack of adequate source evidence is **NOT_READY**;
  any adequate failed source run is **NO-GO**. Even a pass would be only
  reason to investigate, **not** live-game approval.

The real frozen U/V/W trial found **NO-GO** in all three folds. Do not
relabel these newly reviewed sources as sealed independent prospective
test evidence, and do not tune a new threshold using the U/V/W answers
then claim that retuned model passed unseen U/V/W.

## Run privately

Install only free Python dependencies (`numpy`, `Pillow`) and run
locally, **outside the repository** with actual original owner exports:

```bash
OPENBLAS_NUM_THREADS=1 PYTHONPATH=. python tools/m234/evaluate_private_uvw.py \
  --original-review-zip /private/ARGUS_M2.32_UVW_original_blind_review_PRIVATE.zip \
  --owner-labels-dir /private/owner-human-references \
  --private-report /private/ARGUS_M2.34_owner_only_frozen_results.json
```

The output path must not yet exist. It produces private original-step,
human-location and raw per-image diagnostics, PRIVATE fitted coefficients'
SHA and per-fold metrics. **Never** run this command using owner files
inside public GitHub Actions or copy its output to source/Pages.

CI only generates entirely **synthetic** original-sized screenshots
and synthetic source manifests to test identity integrity, SHA mismatch,
wrong owner original step, contradictory duplicate PNG reference,
UNKNOWN/ABSENT controls, strict archive membership and locked NO-GO
gate behavior. Green synthetic tests alone prove software semantics,
not better actual game playing.

## Next science

The decisive failure is not a missing batch of labels: U/V/W independent
development references are complete and evaluated. M2.35 must move
beyond calibration-tuning on known development observations toward
player-identity evidence that can discriminate mage vs hostile lookalikes
and decoration, with an independently valid absence mechanism. A
precommitted experiment may exploit consecutive original screenshot
evidence and long-term appearance consistency without game-internal
state, and must clearly abstain under occlusion/offscreen conditions.
Require independent NEW development whole-run testing before any
sealed one-shot O/P/Q original assessment or live steering activation.
M2.4 original run-01 remains unopened. The unopened 54-frame O/P/Q
prospective owner ZIP SHA is preserved; do not inspect its contents.
