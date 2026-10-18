# M2.22 — Genuine human-labeled offline screenshot localization, not live AI

## Data gate

Private owner I/J/K point exports from M2.20 became available on
2026-10-18. The source-matched private human reference admission passes
the **predefined per-whole-run eligibility threshold** of 3 visible,
3 confirmed absent and 2 off-center player centers for **every run**.
The exact original 54 PNGs and 3 blank reviewers, research seed, full source
ZIP hash and image hashes remain pinned by M2.20/M2.21.

Those human JSON exports and every fitted parameter or frame-level prediction
MUST NEVER be sent to public GitHub CI, Issues, PR descriptions, Pages or
commits. Only generic research methodology and synthetic test fixtures live
here.

## What this slice implements

`tools/m222/offline_localization.py` is a self-contained private CLI
using local Python 3 + free NumPy/Pillow. It verifies the **same** immutable
M2.20 ZIP via `tools/m221/publish_consented_public.py`, and calls the
existing `tools/m220/point_cohort.py` human-reference admission gate,
which verifies every real original screenshot SHA, frame order, labels
and in-bounds coordinates.

- Training positives: exactly the owner-clicked player center on
  VISIBLE screenshots. On ABSENT screenshots there are no invented positives.
  UNKNOWN examples are excluded rather than marked absent.
- Negatives: an immutable sparse grid of image patches on current
  screenshot RGB; exclude candidate patches within 70 original pixels
  of a visible player. Negatives within one screenshot do **not** become
  independent game runs.
- Features: mean luminance, luminance standard deviation, ring contrast,
  strong gradient density and mean saturation measured only from the
  **current screenshot** (53×53 RGB patch; boundary pixels padded).
  No input feature includes step, coordinates, center-anchor role,
  ARGUS guesses, external game internal data, any human answer or
  a classifier response.
- Learning: 1500 fixed-step class-balanced logistic updates, learning rate
  0.12, L2 0.04. Training fold-only normalization.
- Inference: scan an image-wide predeclared fixed 24-pixel grid, choose
  the highest predicted patch with a fixed 0.5 detection threshold.
  Count localization correct if the candidate center is within 35
  original screenshot pixels of the human point.
- **Whole-run-excluded validation:** fit on two whole dev I/J/K
  source sessions, score on the third. Crucially, remove from the
  training data EVERY screenshot whose *exact original PNG SHA256*
  occurs in the excluded run. Two byte-identical static screens were
  found across I/J/K run pairs, so splitting by session alone would
  leak identical images. Repeat for all three folds; train an optional
  final full-development-only research model but never feed the agent.
- Compare model with a deliberately weak center-always-visible baseline.
  Report frame-level presence confusion matrices and number of
  actually localized human-visible players, separately from detection.
  High presence recall is not proof of useful player tracking if every
  ABSENT frame becomes a false positive.

## Private use

Install optional research-only dependencies locally:

~~~bash
python3 -m pip install 'numpy>=2,<3' 'Pillow>=11,<13'
~~~

With owner exports in a PRIVATE local folder, never within a cloned public
repo or any checked-in directory:

~~~bash
python3 tools/m222/offline_localization.py \
  --archive /private/ARGUS_M220_54_Original_Blind_Review.zip \
  --labels-i /private/m220-dev-i-player-points.json \
  --labels-j /private/m220-dev-j-player-points.json \
  --labels-k /private/m220-dev-k-player-points.json \
  --output /private/m222-offline-localization.json
~~~

The report includes private research weights and must remain private.
This is **development-run-out exploratory evaluation**, NOT an
independent prospective held-out test. The original M2.4 run-01 is
never read or used for tuning.

## Scientific outcome and next gate

The first actual private offline run, using the predeclared five-feature
model and all 54 human-reviewed frames, **did not** yield a useful robust
player localizer. It underperformed the center-baseline on true
position correctness and produced false positives on human-confirmed
ABSENT frames. Do not describe the project as having solved screen-based
player detection and do not deploy any of these weights.

The next step is to **design and preregister** a more discriminative,
game-neutral visual representation and collect a fresh prospective
development cohort BEFORE evaluating those changes against its truth.
The historical I/J/K experiment remains a developmental diagnostic.
Do not tune repeatedly against its folds and then claim independent
accuracy. A new untouched prospective cohort and robust
ABSENT/general-position performance are required before shadow/live use.

CI covers only synthetic pixels/labels, validation, run-exclusion and
duplicate-source protection. No real human JSON or image data in CI.
