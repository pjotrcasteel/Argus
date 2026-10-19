# M2.41 — private blinded original screenshot owner review admission

Status: implemented contract; **no owner labels or model accuracy claim**.
Only the already frozen M2.38 three independently captured external browser
sessions are in scope. WyrmForge is an external, unmodified game; no DOM,
script, source, API or model-internal evidence is used.

## Source-of-truth data

Historical GitHub Actions [39242924416](https://github.com/pjotrcasteel/Argus/actions/runs/39242924416)
recorded **m238-a=77, m238-b=77, m238-c=66** original 390x844
PNG screenshots. Its mobile review step failed at a missing Python import
root, **not** at collection, SHA integrity or freezing. M2.40
[PR #82](https://github.com/pjotrcasteel/Argus/pull/82) replayed these
**same preserved** archived original images in mobile Chromium; the old
capture was not substituted with later runs.

Private original artifacts, available for seven days (until **2026-10-26**):

- [original full-browser source and traces](https://github.com/pjotrcasteel/Argus/actions/runs/39242924416/artifacts/12182281756)
- [blank mobile owner review ZIP](https://github.com/pjotrcasteel/Argus/actions/runs/39242924416/artifacts/12182271830)

Keep both ZIPs **outside** the repository and public GitHub Pages. The
review ZIP contains 54 SHA-selected original targets, their 54 authentic
immediately preceding originals, a private offline static HTML labeler,
and `study-manifest.json`. There are only **53 distinct target PNG hashes**:
two original observations are byte-identical, not falsely "54 unique samples".

## Local private workflow

Install free Python 3.12+ and `Pillow`. From ARGUS repository root:

```bash
python -m pip install "Pillow>=11,<13"
mkdir -p /private/m238/source /private/m238/review /private/m238/labels
unzip -q argus_m238_source_20261019.zip -d /private/m238/source
unzip -q argus_m238_review_20261019.zip -d /private/m238/review

PYTHONPATH=. python -m tools.m241.admit_blind_owner \
  --sources /private/m238/source \
  --review /private/m238/review
```

The `/private/m238` paths are **illustrative**; use your actual local
folders (e.g. a private directory in your home folder on Windows/macOS/Linux).
Without actual owner exports, the tool MUST return
`NOT_READY_NO_PRIVATE_HUMAN_LABELS`, `reviewedObservations: 0`,
`unknownObservations: 54`. This is the scientifically correct result.

Review each `annoteren/m238-a/`, `b/`, `c/` page in the private HTML
bundle. Serve it locally, for example:

```bash
python -m http.server --bind 127.0.0.1 8765 --directory /private/m238/review
```

Open `http://127.0.0.1:8765/` **on that same computer**; for an iPhone,
a controlled home-LAN static web server can expose that page to the phone
instead. Avoid public hosting or an untrusted Wi-Fi network: the screenshots
must stay private. Chat file preview does not provide a working live
JavaScript annotation environment.

Mark **VISIBLE** and tap the center of the real *playable* on-screen
player only when confident. Mark **ABSENT** when the playable player is
certainly not visible in the *whole* image (title/town/menu/decorative
mage artwork is not gameplay identity). Mark **UNKNOWN** for ambiguity,
overlays, partial/cropped identity, or uncertainty; never guess, invent
a center or translate UNKNOWN into ABSENT.

The reviewer saves locally. Export three JSON files (unmodified):
`m238-a-player-points.json`, `m238-b-player-points.json`,
`m238-c-player-points.json`. Keep in a **separate private** label folder:

```bash
PYTHONPATH=. python -m tools.m241.admit_blind_owner \
  --sources /private/m238/source \
  --review /private/m238/review \
  --labels /private/m238/labels
```

The command prints **aggregated counts only**; no owner coordinate,
per-image truth, model weights or raw screenshots. It refuses incorrect
study seed, missing/corrupted source, wrong run/order/original SHA,
malformed private JSON, impossible pixel centers, extra fields,
and conflicting definitive labels on the byte-identical screenshot.
Partial submissions and UNKNOWN are tracked as NOT_READY, not fabricated
training truth. A successful full 54-point admission yields
`PRIVATE_BLIND_DEVELOPMENT_REFERENCES_ADMITTED`, **not** a GO claim.

## Explicit research boundaries and next prerequisite

These three runs are **new post-update development evidence**, not
automatically disjoint training + ABSENT calibration + held-out validation.
M2.37 requires **full original chronological sequences** and independently
reviewed **fit:** 8+ VISIBLE and 2+ ABSENT from 2+ runs;
**calibration:** 4+ whole-ABSENT from 2+ OTHER entirely run-disjoint
sources; **evaluation:** 3 NEW held runs, each 8+ VISIBLE and 3+
whole-ABSENT labels. Split by run **and** original image SHA before
fitting. O/P/Q and original M2.4 run-01 remain SEALED.
Do not fit on these 54 targets and later reuse them for an
independent blinded validation.

A source SHA match guarantees screenshot bytes, not that a human
actually made the annotation. Human provenance must remain independently
documented privately. In all cases `m237FullOriginalSequenceReady=false`,
`playerIdentityAccuracyEvaluated=false`, `liveAgentChanged=false`.
When genuine reviews arrive, the next independent decision is whether
this source suffices for *development diagnosis only*, what additional
run-disjoint fit/calibration originals are needed, and how to preserve
future held-out original sequences **before** inspecting predictions.

## Tested and untested

Automated CI uses **invented RGB PNGs and fictional private owner labels**
only. It verifies source SHA/step integrity, absence/unknown abstention,
partial exports, coordinates, exact schema, source tampering, private
duplicate truth consistency and separation from the live agent.
No real WyrmForge pixels, owner JSON exports, sealed sources, model weights
or local review reports are committed or uploaded to GitHub Actions.
