# M2.24 — Public original L/M/N development review only, O/P/Q still sealed

The owner already gave standing permission on 2026-10-18 for publication
of ORIGINAL development screenshot PNGs and completely BLANK mobile
review pages. Do not ask again. This DOES NOT authorize publication of
the owner's actual human labels, evaluation reports, model coefficients,
run traces or unopened prospective images.

## Sources and freezing

Prerequisite: [M2.23 preregistered independent capture
workflow](https://github.com/pjotrcasteel/Argus/actions/workflows/m2-23-preregistered-cohorts.yml)
completes successfully and yields two **different** frozen datasets.

Development source: `argus-m223-original-development-review`,
containing dev-l, dev-m and dev-n with 18 chronologically frozen,
genuine 390×844 screenshot PNGs each plus an embedded BLANK mobile
reviewer. Image choice precedes owner labels and is independent of ARGUS
guesses and inferred game/UI phase.

Prospective source: `argus-m223-prospective-source-SEALED-do-not-open`,
containing dev-o, dev-p and dev-q (54 frozen original PNGs) and their
source-only integrity/provenance manifest. **Never read these source
pixels or publish any prospective sample or review page before the
M2.23 model/preprocessing/threshold are irreversibly locked.**

After the authentic development artifact appears, pin both its exact
ZIP SHA256 and the complete approved public site tree hash before
merging either workflow. A changed source artifact MUST fail.

## Admitted public outputs

Start from the permanent, byte-verified 180-file M2.21 public Git
checkpoint `public-annotator-site-m220-checkpoint`, never from an
expiring Actions artifact or a remote live page.

Only add exactly:

- 54 original SHA256-verified dev-l/m/n screenshot PNGs, 18 each;
- 3 completely UNKNOWN local-first annotation HTML pages, 18 frames
  each, using independent `argus-m223-player-point-` localStorage
  keys and downloadable private JSON;
- 3 homepage links and a public manifest mapping approved development
  filename to original PNG SHA256.

The resulting site has **237 files total:** 216 original PNGs, 18
blank mobile reviewers, homepage, public digest manifest and empty
`.nojekyll`. Every existing page/image is preserved byte-for-byte
except the homepage and public digest manifest. The frozen raw research
plan (contains private provenance/trace SHA) is NOT published.

The verifier `tools/m224/publish_development_only.py` rejects any
private label export, model weight, raw `.jsonl`, original M2.4
held-out `run-01`, sealed dev-o/p/q image, extra file, altered source
PNG or an original reviewer with non-UNKNOWN human state.

## Publication and use

Create a NEW non-force permanent Git site-checkpoint branch after
passing both the exact original source and the new complete public-tree
hash guard. Then switch GitHub Pages to that new branch in a separate
PR and run the **entire old+new mobile Chromium suite**.

Once successful, the user can review on iPhone:

- https://pjotrcasteel.github.io/Argus/annoteren/dev-l/
- https://pjotrcasteel.github.io/Argus/annoteren/dev-m/
- https://pjotrcasteel.github.io/Argus/annoteren/dev-n/

Human labels remain local until the user explicitly uploads the three
JSON files to the private conversation for analysis. Never use these
reviews to retrospectively resample images or open O/P/Q.
