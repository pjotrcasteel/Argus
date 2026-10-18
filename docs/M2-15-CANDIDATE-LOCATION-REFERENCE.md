# M2.15 — A true candidate-LOCATION human-reference protocol (private, blind)

## Why M2.14's frame-wide review is not the answer

On **three genuinely new independent WyrmForge browser sessions** the original
M2.10 shadow had **1 / 2 / 3** raw temporary player memories (6 total).
Only **0 / 1 / 2** fell into the 24 already frozen blind images per run.
Even perfect human review of all 72 M2.14 images cannot yield enough
local shadow proposals to satisfy the M2.13 >=10 per run readiness gate.

M2.15 changes what humans evaluate, rather than quietly lowering that
gate, fabricating negatives or claiming the visual AI can already learn
from real gameplay.

## Precommitted input / sampling (before any new labels)

Input is the **unchanged original M2.14 full-development evidence artifact**
[39106267438](https://github.com/pjotrcasteel/Argus/actions/runs/39106267438)
with three different trace SHA-256s (41 F / 73 G / 44 H frames).
It must still contain exact development-only M2.14 frozen
`study-plan.json` (seed
`argus-m214-blind-24-bins-development-only-20261018`, 24 steps/run).
No `run-01` held-out may be read or repurposed.

Fixed M2.15 choices:

| Protocol dimension | Frozen specification |
| --- | --- |
| Study seed | `argus-m215-independent-spatial-probes-20261018-v1` |
| Run IDs | dev-f, dev-g, dev-h (separate original browser sessions) |
| Eligible frames | Only `exploring` from the *already frozen* 24 original M2.14 frames/run |
| Frame selection | Exactly 12 chronological bins of eligible frames, fixed SHA-256 seeded draw/bin |
| Pixel coordinates | 390 × 844 original screenshot, no resizing of source PNG bytes |
| Probe #1 | fixed center `(195,422)` on each selected original frame |
| Probe #2 | 1 predeclared SHA-256-selected position from 8 fixed peripheral grid cells |
| Human question | Is the player visible **within 26px of this exact circle center**? |
| Human decisions | `present`, `absent`, `unknown` ONLY |
| Total workload ceiling | 12 images × 2 circles × 3 runs = 72 local decisions, 36 original PNGs |
| Model use | NONE. No player guesses in frame sampling, labels, HTML or live policy |
| Publication | **Private artifact only**, never auto-publish these unapproved F/G/H images to Pages |

Sampling uses neither RGB detection, manual annotation, player position,
game outcomes, threat candidates nor model output. The one center
probe and one peripheral control are **not automatically assumed**
positive/negative — a human judges both. Each whole original play
session is a correlated unit, even though it supplies multiple
candidate locations; adjacent screenshot samples are not independent
runs. A deliberately restricted center/grid distribution has its own
selection bias: eventual classification results cannot be interpreted
as real-world performance on arbitrary positions or other games.

The original screenshots are copied **byte-for-byte** and verified with
SHA-256. Only the selected 36 screenshots, fixed circle coordinates
and entirely unknown annotation schema appear in mobile HTML. The
full trace/agent telemetry, original human ground truth, hidden game
state and any AI predictions NEVER go into review pages. The complete
study plan is separately saved as `spatial-study-plan.json`.

## Mobile review semantics and integrity

`web/candidate-annotator/index.template.html` shows one original
screenshot and one **yellow 26-pixel radius** circle at a time.
The reviewer answers *here*, *not here* or *unknown*.
The same screenshot appears twice with distinct preregistered
center/control points. It must be correct to mark a point ABSENT when
the real player is visible elsewhere.

Human decisions begin `unknown`; only actual clicks are saved
in browser local storage. JSON download/import work without external
services. Each candidate has a stable sample ID, original screenshot
step and immutable x/y/role/radius. Import and the offline
`validate` command reject duplicates, altered positions, unknown
label values, different studies, wrong run IDs, missing rows and
extra fields. Unknown labels are not silently treated as false.

**No training happens here**: `present` and `absent` are human
references, not sampled machine guesses. This new candidate
dataset is deliberately not passed off as old
`BRIDGE_ONE/BRIDGE_TWO` data. Doing so would fabricate a temporal
feature (`secondGap`) and compromise the evaluation. M2.16
must introduce a proper candidate-level featurizer/learner
with no made-up bridge states, independent run splits, and a
readiness gate measured on genuine owner labels.

## Reproducibility: real evidence

```bash
python3 tools/m215/spatial_candidate_review.py build \
  --archive ARGUS_M2.14_FGH_Full_Development_Evidence.zip \
  --output ./private-m215-review

# After manual review, validate one exported JSON without public upload
python3 tools/m215/spatial_candidate_review.py validate \
  --manifest ./private-m215-review/spatial-study-plan.json \
  --export ./private-m215-review/annotated-dev-f.json
```

The original authentic source archive is currently stored in the
private conversation workspace, and [M2.14 Actions source
artifact](https://github.com/pjotrcasteel/Argus/actions/runs/39106267438/artifacts/12129085171)
has short retention. The main-merge workflow
`.github/workflows/m2-15-private-review.yml` retrieves that
original, verifies every original PNG against SHA-256, tests a
touchscreen Chromium browser on all three local pages, then uploads
only a **short-lived private-ready-to-host Actions artifact**.
It does **NOT** publish new public URLs. The user explicitly
approved publication for older C/D/E but NOT F/G/H; ask again
before extending GitHub Pages.

The general CI uses only artificial original-format PNGs and
fake image traces; its pass demonstrates implementation, not
genuine player absence discrimination. The standard external
black-box WyrmForge smoke must also pass before merge.

## Decision gate after this slice

Only after the authentic reviewer is verified and the annotated
data source is suitably accessible should we ask for the
owner's *optional* 72 local-position decisions. If the owner
wants iPhone links, request **explicit publication approval for
the 36 newly selected original WyrmForge screenshots**; the
new blind study wasn't covered by earlier consent. Until then,
keep the ready-to-host ZIP in the conversation and/or short-lived
Actions artifact.

No `run-01` tuning; no claimed player identity model;
no gameplay activation; no automatic `RESEARCH_TRAINED`
on synthetic features; never upload genuine private human labels
to public GitHub.
