# M2.23 — Preregistered multiscale image appearance + six new black-box sessions

**Status before the first real M2.23 captures:** the image representation,
calibration protocol, rejection thresholds, group splitting and SHA-seeded
sampling rules below are frozen in code. Only artificial PNGs/labels may be
used by CI to test implementation. Existing private I/J/K owner answers and
scores are **NOT** a hyperparameter search target. The old five-feature
experiment failed on presence and location and was never deployed.

## Model hypothesis and limitations

The M2.22 five global numbers per local image crop cannot distinguish a
character silhouette from colorful terrain or menus. M2.23 tests a richer
**246-dimensional screenshot-only local descriptor**, not a game-specific
RGB palette:

- A 64×64 region around each candidate with edge-repeat padding at borders;
- 4×4 spatial cells, each containing three RGB averages and three RGB
  standard deviations (96 features);
- 4×4 eight-bin gradient orientation histograms, normalized as a group
  (128 features), and each cell's edge-magnitude average (16 features);
- Whole-region RGB average and deviation (6 features).

`tools/m223/multiscale_appearance.py` contains **no WyrmForge character
sprites, class names, assets, coordinates, game code, health, enemy
telemetry, OCR or hidden player events**. Pixel positions are used only
to sample crops; never appended to model inputs.

Its fixed research baseline uses a class-balanced, L2-regularized
deterministic linear logistic classifier trained with 1000 iterations,
step size 0.08 and L2 0.08. Explicitly labeled VISIBLE frames generate
one positive patch at the owner's center. Negatives come from a frozen
sparse whole-screen grid at least 70 px away from the true center.
UNKNOWN frames contribute nothing. Training frame SHA256 deduplication
prevents identical source images being counted repeatedly.

This study **requires at least three human-confirmed player-ABSENT
training whole images** to calibrate the score threshold, in addition
to three positive images. The admission condition cannot be satisfied
by labeling a random background patch as a player-absent screenshot.
The score threshold is the maximum of 0.5 and the **maximum predicted
candidate score on whole ABSENT training images plus 0.025**, capped
at 1.0. This is deliberately conservative and may reject many true
players. It must not be relaxed by looking at later test answers.
Image-wide scan uses predeclared 20px candidate spacing, chooses
the highest candidate or returns ABSENT. Within 35 original pixels of
the owner's point counts as correct localization. Presence/absence
confusion matrix is reported separately from location accuracy.
Compare against a center-always-present baseline and original M2.22.

## Two never-shared research roles

`tools/m223/freeze_new_cohorts.py` froze TWO separate sampling seeds
before any source images or owner reference labels were collected:

- **Development**: dev-l / dev-m / dev-n, seed
  `argus-m223-fresh-development-20261018-v1`. Each original external
  Chromium game run is separated; 18 deterministic hashed chronological
  time bins sample original 390×844 screenshots, regardless of whether
  ARGUS guessed the player, OCR phase, enemy, action or outcome. Only
  these three runs have a blank mobile owner-reference reviewer.
- **Sealed prospective**: dev-o / dev-p / dev-q, seed
  `argus-m223-sealed-prospective-20261018-v1`. A different set of
  three independently captured original browser sessions; 18 same-type
  predeclared original frame bins each. Their frozen screenshot bytes
  and only a provenance/selection manifest are stored separately.
  **No reviewer HTML, no human labels, and no training authorization.**
  Never open these images or derive labels, thresholds, parameter
  choices or hyperparameters from them until after the representation
  and model are fully locked for prospective evaluation.

Both parts require 36–180 truly captured original frames/run, three
distinct whole-run SHA256 traces, successful agent process/visual
evidence check, exact original PNG dimensions, honest unknown external
game outcome/version and original chronological frame indices.
There is no fake synthetic source data in the real capture job.

The GitHub Actions job does not ask WyrmForge for special source data,
no game DOM/API, no mandatory paid AI services. It uses ordinary
Playwright screenshots plus keyboard/mouse through existing .NET 10
ARGUS. Game source is never changed.

## Leakage barriers

Whole-run-out folds exclude the entire evaluation run from fitting,
scaling and image-ABSENT calibration. Any exact original screenshot SHA
appearing in test must also be omitted from all training other runs,
including static identical menus. The original historical M2.4
`run-01` is **never** opened or touched.

The prospective partition remains sealed, even if someone is tempted
to label it early. Development images/screenshots and blank reviewers
are within the owner's existing standing permission to publish, but
the M2.23 capture workflow initially provides them as a **temporary
private artifact**; any public Pages publication must be separately
reviewed for exact image hashes and no trace/label leaks.

No M2.23 real owner human labels, model coefficients, predictions,
source traces or prospective screenshots may be published with
GitHub Pages or committed to main. The synthetic-only test job checks
features, image-size boundaries, training absent gate, different
run image SHA decontamination, and honest refusal to access held-out
or prospective original source files.

## Go/no-go criteria

Do **not** call the system intelligent merely because the new model
produces positive predictions. A candidate useful model must show
credible player-localization accuracy and materially lower false
positive presence on entirely human-ABSENT frames, with transparent
per-run confusion matrices and comparison to the deliberately weak
center heuristic. With only three development runs and many correlated
neighboring screenshots, any finding is preliminary.

Train and calibrate using **new L/M/N owner-reviewed development only**
after authentic source and human annotation SHA verification. Lock
exact model weights, preprocessing, score threshold, allowed input and
metrics **before** revealing or evaluating O/P/Q. The sealed prospective
set must have independent owner truth and no extra tuning once opened.
Keep runtime gameplay policy entirely unchanged until prospective
behavior is convincing.

After merge: inspect genuine six-run capture logs and validate
frozen original development screenshots only. Arrange three blank
iPhone reviewers for L/M/N under standing owner consent, without
ever exposing O/P/Q. New labels must remain private, just like I/J/K.
