# ARGUS M2.8 — Human-visible threats missing from detector evidence

## Real development-only discovery (private owner review, 2026-10-17)

The owner completed **all 36 frozen screenshot annotations** for three NEW
development-only WyrmForge runs `dev-c`, `dev-d`, `dev-e`, preserving
the original study selection, including one unknown threat-reference frame
in each run. **Actual human `labels.json`, all exact coordinates and
full private traces/screenshots remain outside public GitHub.** The
original M2.4 held-out `run-01` has not been accessed.

An independent local **Python mirror of the checked-in C# trace scorer**
(greedy one-to-one pairing at 26 pixels), using original saved ARGUS
predictions, gives:

| New development run | Comparable combat frames | TP | FP | FN | Precision | Recall |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| dev-c | 7 | 35 | 12 | 10 | 74.5% | 77.8% |
| dev-d | 7 | 28 | 18 | 13 | 60.9% | 68.3% |
| dev-e | 7 | 27 | 26 | 13 | 50.9% | 67.5% |
| **Pooled candidates** | **21** | **90** | **56** | **36** | **61.6%** | **71.4%** |

These 21 compared frames are **correlated within three play sessions**;
pooled percentages describe this development sample only, not a held-out
generalization estimate. No learned model, production .NET run on
human reference or active policy change is claimed. A
separate private report stores all matching details and exact labels.

**Crucial evidence-coverage issue:** the owner marked **148** visible
threat centers. Only **126** were on screenshots with an actual recorded
`evidence.combat.threatLocations` array; the other **22** occurred on
FOUR human-reviewed screenshots for which no comparable detection
evidence exists:

| Development run | Frozen step | Recorded phase | Human-marked threat centers |
| --- | ---: | --- | ---: |
| dev-c | 30 | transition | 6 |
| dev-d | 18 | exploring | 7 |
| dev-e | 31 | transition | 6 |
| dev-e | 39 | exploring | 3 |

All four must remain **UNAVAILABLE**, not zero predictions or false
negatives, in the published observed-frame quality score. This
simultaneously warns readers **the recall denominator excludes hard
unobserved frames**: a visually missing or unmeasured player is not
equivalent to having no visible monsters. Some transition screens may be
genuine mode switches, so this table does not prove a classifier defect.

Of 36 false-negative centers on the **comparable** frames, **21**
have coordinates outside the original central RGB search region
(roughly horizontal 1/30–29/30 and vertical 19%–82%). Geometry alone
cannot establish whether a shape is a genuine enemy, particle effect
or correctly split sprite.

## Small, safe engineering fix

`PerceptionScoring.ScoreAsync` now computes two explicit, additional,
non-classification counters using **known human threat coordinates** only:

- `ThreatCentersOnUnavailableFrames`: number of human-marked visible
  enemy centers on labeled frames where the original trace never
  produced comparable threat positions, or is an unverified legacy
  final frame.
- `ThreatFramesWithHumanCentersAndUnavailableEvidence`: count of those
  frames with at least one human mark.

The original `ThreatEvidenceUnavailableFrames`, true positive/false
positive/false negative and precision/recall calculations remain
unchanged. A frame with an `unknown` human threat state contributes
nothing to the new counters. An unavailable frame with a human-reviewed
`known` *empty* threat list still increments the original unavailable
frame count, but **not** these positive-mark counters.

The new counts are printed in the offline CLI, the single-run
`perception-report.md`, and the aggregate **study quality table**
to keep data quality visible alongside accuracy figures. No game
input, segmentation, image-label inference, or learning algorithm
changes. Deterministic synthetic MSTest exercises two unavailable
frames with seven human points plus a third properly scored frame.

## Reproduce on the owner's private machine

The original M2.7 screenshot evidence and 36 user annotations are
preserved outside this repository. On a local machine with the .NET
10 SDK, put each `labels.json` next to its matching real trace and
PNGs in the three original folders:

```bash
dotnet run --project src/Argus.Study.Quality.Tool -- --runs PRIVATE_M27_ROOT
dotnet run --project src/Argus.Evaluation.Tool -- --run PRIVATE_M27_ROOT/dev-c --labels PRIVATE_M27_ROOT/dev-c/labels.json
dotnet run --project src/Argus.Evaluation.Tool -- --run PRIVATE_M27_ROOT/dev-d --labels PRIVATE_M27_ROOT/dev-d/labels.json
dotnet run --project src/Argus.Evaluation.Tool -- --run PRIVATE_M27_ROOT/dev-e --labels PRIVATE_M27_ROOT/dev-e/labels.json
```

Expect **22 human-marked threat centers on four unavailable frames**
when no underlying files are modified; verify the official .NET totals
against the independently reproduced development results above
before drawing further conclusions. Run the M2.6d causal appearance /
shadow audit only privately and treat its color blobs as candidates,
not verified sprite identities. For classifier exploration, require
independent run-level data balance and a fresh, untouched held-out
cohort; never tune to the old held-out.
