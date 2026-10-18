# M2.20 — New independent development-only source screenshots + blind player-center cohort

## Scientific reason

The private M2.18 F/G/H cohort from 36 original user-reviewed images did not
satisfy per-run human-ABSENT and off-center visible sample sufficiency.
Never silently invent absent labels, use ARGUS's guesses as truth or tune
against original held-out run-01.

## Implementation

The new first-merge-once and otherwise manual-only GitHub Actions workflow
is .github/workflows/m2-20-development.yml. It captures three new separate
external game browser processes, dev-i/dev-j/dev-k, with screenshots and normal
mouse/keyboard through unmodified .NET 10 ARGUS. Each is bounded to 76 steps
or 190 seconds and records honest trace/game/profile provenance, with unknown
outcome and unknown external game version.

tools/m220/point_cohort.py uses exactly 18 chronological screenshot bins per
original whole run with fixed SHA256 seed:

argus-m220-independent-blind-player-point-20261018-v1

The sampler NEVER looks at agent player position, sprite confidence,
moving threats, inferred UI phase, game outcome or human labels. Diagnostic
phase counts are calculated ONLY after frame selection. Sampling across
all observed gameplay phases avoids the old exploring-only restriction but
cannot GUARANTEE any real absent player or player away from screen center.

The mobile reviewer reuses the validated M2.18 original pixel-coordinate
tap/circular SVG UI, extended to 18 screens and the independent I/J/K IDs.
All locations begin UNKNOWN. The owner marks VISIBLE with an exact tap,
ABSENT explicitly or UNKNOWN. All references stay in browser localStorage
until owner downloads a private JSON. There is no human-label upload endpoint.

## Nonpublication/privacy

The M2.20 screenshot review ZIP is NOT automatically added to public Pages.
A future publication of any new images needs a separately verified, approved
snapshot. The permanent 122-file M2.19 public site stays unaltered.

The new workflow keeps only short-lived 7-day original source and BLANK
reviewer artifacts. NO owner labels, private fitted weights, game internals
or original M2.4 run-01 held-out content enter the workflow or repository.

## Private reference audit

Run locally with the extracted blank reviewer bundle after human review:

    python3 tools/m220/point_cohort.py audit --site /private/m220-private-review
      --labels-i /private/m220-dev-i-player-points.json
      --labels-j /private/m220-dev-j-player-points.json
      --labels-k /private/m220-dev-k-player-points.json
      --output /private/m220-reference-coverage.json

Requires >=3 VISIBLE, >=3 human-confirmed ABSENT and >=2 off-center visible
player centers per independent run. The output is STRUCTURALLY_ELIGIBLE or
NOT_READY. This is reference sufficiency only, NOT detector precision/recall,
NOT independent images within a run, NOT a trained AI and NOT live policy.

## Tests

Synthetic valid PNG/trace/label tests enforce unbiased deterministic
sampling, source-step SHA integrity, held-out rejection, and honest
readiness. Mobile Chromium verifies each original image, touch coordinates
on three viewports, round SVG marker, ABSENT/UNKNOWN, download/import/reload.

After a successful real capture, inspect the original screenshot/trace
artifact and the frozen selection. User review of the new independent
screenshots precedes any development-run-grouped offline model fitting.
Keep original M2.4 held-out run-01 untouched.
