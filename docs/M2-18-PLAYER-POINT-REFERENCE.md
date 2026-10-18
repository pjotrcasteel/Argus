# M2.18 — Blind player-center reference without center-anchor bias

## The problem

The original M2.15 development F/G/H study presents a fixed central circle
and one spatial-control circle for each image. This can validate a narrow
yes/no claim, but cannot reveal the true player location when the avatar is
elsewhere, and its positives are structurally overrepresented near screen
center. Training on those binary labels alone cannot establish a general
position-independent player detector. The owner also found a M2.15 distorted
marker; PR #48 fixed round SVG display and the owner confirmed iPhone Safari.

M2.16 offline classifier remains **untrained on actual owner labels** because
one original dev-f step-27 human reference is contradictory. M2.17 created
a private, owner-controlled, one-frame re-review. This problem is deliberately
kept separate from M2.18: don't change existing labels automatically.

## What this milestone delivers

- Use the *exact* approved ORIGINAL M2.15 source ZIP (pinned full archive
  SHA-256, 36 immutable original screenshots, three distinct development
  sessions and fully UNKNOWN source labels). No traces, held-out images,
  predicted player centers, outcome information or gameplay/source integration.
- Add three new public **BLIND** mobile review pages, reusing the existing
  36 published 390×844 PNG files **in place**; zero new images. All original
  nine reviewers, the old homepage links, original screenshots and the old
  published-review route manifest remain intact.
- Ask the owner to **tap the true player center anywhere** in each original
  screenshot; or mark the player ABSENT or UNKNOWN. A single image-space SVG
  ring, scaled uniformly, shows the chosen point and won't become oval.
- Persist only in device localStorage. JSON export/import remains entirely
  local; no HTTP annotation uploads and no ARGUS model guesses included.
- Provide a local, strict private owner-export reader: verify original
  screenshot steps/hashes, reference run/seed, enum and point coordinates.
  For a visibly located player, emit its human-selected positive patch plus
  a fixed set of spatially disjoint negative windows (>=70 image pixels
  away). A human-confirmed absent screenshot produces only negatives.
  Unknown screenshots produce no training examples. No negatives are
  automatically derived from uncertain player state.
- Require per-run class and **off-center visible player** coverage before
  reporting STRUCTURALLY_READY. This is merely an input quality indicator:
  no ML weights, learning score, model deployment or live game action.

## Production review links (when Pages CI + deployment succeed)

- https://pjotrcasteel.github.io/Argus/annoteren/dev-f-points/
- https://pjotrcasteel.github.io/Argus/annoteren/dev-g-points/
- https://pjotrcasteel.github.io/Argus/annoteren/dev-h-points/

These are a **new** study. They neither import nor alter previous M2.15
circle labels. The user is not expected to annotate both studies immediately.
The original already-public development screenshots are approved for public
review, but private results must stay on the owner's device or private machine.

## Local-only private audit command

~~~bash
python3 tools/m218/player_point_study.py audit \
  --archive /private/M215-original-source.zip \
  --labels-f /private/m218-dev-f-player-points.json \
  --labels-g /private/m218-dev-g-player-points.json \
  --labels-h /private/m218-dev-h-player-points.json \
  --output /private/m218-point-study-audit.json
~~~

This derived dataset contains *multiple patches per source screenshot*:
they are **correlated** and cannot be used as independent game samples.
All F/G/H runs are still a single development cohort from the historical
M2.14 sampling plan. Explicitly report image/frame/run denominators and
whether off-center positive evidence really exists before a future M2.19
model-evaluation design. Do not repurpose the original M2.4 held-out run-01
or call the checks prospective; future independent runs are still required.

## Validation and safety

The static site publisher fails closed if the original M2.15 ZIP, image hashes,
source partitions or developer run IDs change. It compares all existing
public reviewer HTML/PNG bytes before and after appending new routes, and
verifies that no extra PNG was created. Unit tests are **100% synthetic**;
mobile Chromium tests load the genuine approved original 36 PNGs only in
a public page build and verify touch coordinate precision at 320/375/430,
correct SVG geometry, player ABSENT/UNKNOWN, JSON download/import and reload.
The active ARGUS gameplay agent and persona/motion logic are unchanged.

Next engineering work after validated M2.18: record a genuinely fresh
independent development cohort, ground true player centers with this method,
measure spatial diversity and alternative appearance cues on entire
held-out development sessions. Only after reliable prospective performance
should an image learner be considered for a bounded shadow rollout.
