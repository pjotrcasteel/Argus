# M2.25 — Private L/M/N human-reference admission and locked M2.23 HOG evaluation

## Source and privacy contract

Genuine private owner exports for dev-l, dev-m and dev-n were received
2026-10-18. `tools/m225/private_source_study.py` uses ONLY:

- the previously pinned original **development-only** M2.23 review ZIP, with
  SHA256 `400b9df3233ab0cf1af20d2e57d9cef84ffb48bbdd7a40f98bc4d4700f1c303d`;
- three **private local** owner-exported JSON references (never put in Git);
- the **unchanged preregistered** image model code at
  `tools/m223/multiscale_appearance.py`.

It verifies the complete 59-member original ZIP, allowed filenames,
raw PNG 390×844 dimensions and SHA, chronological frame steps, run IDs,
frozen original source seed, private JSON schema, strict pixel center
types/bounds, UNKNOWN/ABSENT semantics and at least 3 visible,
3 genuinely absent and 2 off-center visible player locations **per run**.
Contradictory human states/coordinates on byte-identical original
screenshots are refused. The original repeated still image must not
be double counted as independent image evidence.

The sealed prospective O/P/Q research artifact must remain **unopened**;
the original M2.4 held-out `run-01` is never accessed. Human exports,
frame-level truth, fitted model weights and individual scores MUST NOT
enter Git commits, Issues, PR bodies, CI, Pages, model registry or runtime.

## Local usage only

~~~bash
python3 -m pip install 'numpy>=2,<3' 'Pillow>=11,<13'
OPENBLAS_NUM_THREADS=1 python3 tools/m225/private_source_study.py \
  --archive /private/ARGUS_M223_54_Original_Development_Blind_Review.zip \
  --labels-l /private/m223-dev-l-player-points.json \
  --labels-m /private/m223-dev-m-player-points.json \
  --labels-n /private/m223-dev-n-player-points.json \
  --output /private/ARGUS_M225_Private_Frozen_HOG_Results.json
~~~

The command refuses to overwrite a private result. It trains and
calibrates independently on two whole source runs; removes any exact
image SHA that also occurs in the left-out run before constructing
training patches; uses the M2.23 frozen 246-feature RGB/HOG representation,
fixed 20px grid, fixed class weighting, 1000 iterations, regularization
and training-only human-ABSENT whole-image calibration. It separately
scores presence/absence and correct actual pixel location (35px radius)
against a fixed always-present center-position baseline.

After three left-out development folds, it fits one final
**development-only** research model, with a canonical SHA256 of its
complete coefficient/normalization/threshold JSON. Its results MUST
remain on the owner's private device. This does **not** authorize
runtime activation.

## Observed result and no-go

All 54 original L/M/N owner annotations passed independent SHA and
class/location coverage validation. Each independent run has 12 VISIBLE,
6 human-ABSENT and at least 4 true off-center player positions. One
repeated original PNG digest was correctly detected within development
references (53 unique original image hashes). There is no cross-run
identical image leakage among the three M2.25 development folds.

The **preregistered** logistic/HOG learner's human-ABSENT score
calibration saturated to a threshold of **1.0 in every fold**. Because
the scoring sigmoid cannot exceed 1, all 36 visible player frames
were rejected, with zero false positives on the 18 absent frames:

- Image model: **0/36 correct localizations**, 0/36 visible presence
  detections and 18/18 absent screens correctly rejected.
- Always-center baseline: **23/36 true locations** but **18/18 false
  positives** on absent screenshots.

The all-absent behavior does **not** constitute useful player detection.
This is a **NO-GO** and materially worse at actual localization.
The fitted development-only model is not enabled in live ARGUS.
Changing threshold or representation after looking at these development
folds and claiming their score as new independent evidence is forbidden.

## Scientific next gate

M2.26 should address the identified **image-wise multiple-comparisons/
absence threshold saturation** with a separately fixed false-positive
control and better high-confidence candidate representation. Freeze
model parameters and source/label integrity with development data
*before* accessing sealed O/P/Q prospectives. After selecting ONE
final challenger **without using prospective evidence**, document
exact weights, preprocessing, thresholds, and canonical hash, and
evaluate O/P/Q only once with independent human truth. If the
training-only development evidence is insufficient, report that
instead of opening/tuning on the prospective cohort.

The prospective O/P/Q ZIP has a separate private copy, must not be
opened or published, and the temporary GitHub artifact expires
2026-10-25. Synthetic CI tests fake PNG/owner data only and does not
see actual user annotations.
