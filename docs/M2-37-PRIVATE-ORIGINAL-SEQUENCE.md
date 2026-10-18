# M2.37 — private authentic original-sequence replay (research only)

## Scope and provenance

M2.34 produced a three-run **NO-GO** on authentic R/S/T and U/V/W
development evidence. M2.35 introduced source-only causal multi-track
hypotheses; M2.36 fitted a contrastive image-patch metric using training-only
human positive crops and difficult negative backgrounds.

M2.37 connects those **offline** research modules. This is not an in-game
detector, autonomous trained player, win-rate evaluation, live spectator, or
new WyrmForge integration. The active ARGUS visual steering remains untouched.

WyrmForge is a changing **external** target. On 2026-10-18 its gameplay
environment was updated by #120 and #121 (town/start flow, Arcane Nexus,
mobile layout and recap). **Never assume an old screen/UI layout, source
version or route position describes the next capture.** Independently
capture and attest a game-version/commit if possible from public evidence;
otherwise preserve `gameVersion: "unknown"` rather than guessing.

## Local, private input contract

Run from repository root after installing free Python, Pillow, NumPy:

    PYTHONPATH=. OPENBLAS_NUM_THREADS=1 python -m tools.m237.original_sequence_replay --manifest /private/m237/manifest.json --output /private/m237/replay.json

All originals and future human labels live **outside the public repository,
Actions and Pages**. The tool does not download the game, start Chromium,
upload model data, drive a game, or follow its internal JS/DOM. Paths must be
local relative 390x844, one-frame unmodified RGB **PNG**. Every screenshot
has an actual original PNG byte SHA-256 and an authoritative session/step.

Private `manifest.json` schema (illustrative names/digests only):

    {
      "schemaVersion": 1,
      "studyType": "NEW_SOURCE",
      "gameVersion": "unknown",
      "fit": [
        {"run": "fit-a", "step": 0, "path": "fit-a/000.png",
         "sha256": "<real lowercase SHA256>",
         "state": "visible", "center": {"x": 180, "y": 410}}
      ],
      "calibration": [
        {"run": "cal-a", "step": 0, "path": "cal-a/000.png",
         "sha256": "<real lowercase SHA256>",
         "state": "absent", "center": null}
      ],
      "runs": [
        {"runId": "new-a", "frames": [
          {"run": "new-a", "step": 0, "path": "new-a/000.png",
           "sha256": "<real lowercase SHA256>"}
        ]}
      ]
    }

The illustration above is not sufficient data for fitting or calibration.
The full original fitting set requires >=8 reviewed VISIBLE positives,
>=2 true-ABSENT fitting images, >=2 fitting runs. Whole-frame calibration
requires **>=4 original human-ABSENT** images from >=2 *other* entire runs,
with **no** exact image SHA or run overlap. Evaluation requires entirely
separate runs and original SHA values. NEW_SOURCE requires >=3 new runs, and
previously examined R/S/T/U/V/W cannot be relabeled as prospective.
The sealed O/P/Q original cohort and earlier M2.4 run-01 are explicitly
rejected by the private manifest loader.

Every replay frame forbids state, player center and model predictions.
Original chronological order is kept exactly as supplied; no sorting,
frame interpolation or gaps that extend an existing visual identity.
An unpaired first/gap screenshot may produce an appearance proposal but
cannot supply fabricated previous-image change.

## Locked pre-annotation decision rule

1. Rank M2.36 image-wide learned patch candidates using its original
   trained exemplar metric and separately calibrated genuine ABSENT maximum
   plus the fixed .02 margin. Ambiguous near-equal top appearances abstain.
2. Link candidate patches using M2.35 one-to-one appearance trajectories:
   3 authentic consecutive observations, >=2 changed transitions, matching
   original predecessor PNG SHA, displacement <=120px, cosine >=.86, and
   mature lookalike contrast >=.40. Scene motion is never proof of mage.
3. Research acceptance additionally requires same top appearance and
   mature temporal candidate center, immediate original predecessor,
   local actual frame change >=.012 and center at least 24px from all
   screenshot borders. Unknown gaps/edge crops, static graphics, lack of
   change, conflicting candidates and ambiguous lookalikes **abstain**.
4. An accepted point is `SHADOW_ACCEPTED_HYPOTHESIS`, never a verified
   player. Every output still has `playerPresent: null` and
   `verifiedPlayerCenter: null`. No active policy consumes these.
5. Include original byte SHA for each replay step and the SHA-256 digest
   of the fitted model and frozen calibration threshold, without writing
   model weights, owner labels or source pixels to the public repo.

**No retroactive tuning after looking at the future owner review.**

## Future evaluation gate

After three independent new external whole-game sessions are recorded with
ordinary player inputs, freeze original SHA targets, sample without seeing
ARGUS proposals, then request truly independent owner-visible-center,
ABSENT or UNKNOWN labels in a separate local JSON file:

    {
      "runs": [
        {"runId": "new-a", "frames": [
          {"step": 0, "sha256": "<same actual original SHA256>",
           "state": "unknown", "center": null}
        ]}
      ]
    }

Score only locally:

    PYTHONPATH=. python -m tools.m237.original_sequence_replay --manifest /private/m237/manifest.json --human-labels /private/m237/review.json --output /private/m237/reviewed-report.json

The reference must include **every original replay step** and exact matching
SHA, and any unknown must remain unknown (never counted as negative).
Per newly independent held run require >=8 VISIBLE and >=3 true-ABSENT,
>=50% **accepted within35px** among all reviewed visible, and <=25%
false acceptance on true-ABSENT. All three new runs must independently pass
for `PRELIMINARY_GO_RESEARCH_ONLY`. Insufficient reviewed coverage produces
`NOT_READY`; failure produces `NO_GO`. Historical runs can only return
diagnostic findings. A preliminary go permits further blinded research,
not live player identity, competitive success, or game-balancing changes.
Neither the tool nor synthetic CI proves that a supplied label was made
by an independent human; provenance remains a private procedural control.

## Verified and not verified

The dedicated public CI is deliberately limited to completely invented
RGB PNGs, tests for original bytes, run-level disjointness, calibrated
appearance/temporal agreement, gap abstention, sealed-source refusal, and
offline human-reference accounting. It carries **zero original WyrmForge
screenshot, model artifact or owner label**.

Next: validate the private tool on authentic chronological R/S/T and U/V/W
**only as known development diagnostics** when complete source predecessors
are available. Then capture genuinely new post-update WyrmForge sessions
with immutable provenance and obtain owner-blind labels. Do not open
M2.23 O/P/Q or the M2.4 original run-01. No live-game steering changes.
