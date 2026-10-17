# ARGUS M2.12 — Make the difficult negative examples visible before training AI

## Research question

M2.10 and M2.11 showed a gap between a **plausible** stale player memory and a
**verified** player. M2.11 correctly vetoes an almost uniform empty 49x49 patch,
but leaves both a red textured empty scene and a large blue player-palette
imposter undecidable. It must NEVER translate texture or player-like color into
proof of identity.

M2.12 does not keep piling on recognition heuristics. Instead, it creates a
**small, reproducible, explicitly grouped future-learning dataset** of the
current-frame visual evidence that the existing model cannot resolve.

## Production code and exact boundaries

`PlayerShadowHardNegativeEvidence` summarizes the original
`PlayerCurrentFrameVetoReport` into mutually exclusive, audited groups:

- **Correctly vetoed absence**: human confirmed player absent and current
  screenshot had the existing `EMPTY_PATCH_VETO`.
- **Unresolved textured absence**: human marked player absent, zero qualifying
  player-palette pixels in the sampled region, but texture/RGB variation
  prevents the conservative empty-patch veto.
- **Unresolved player-color lookalike**: human marked player absent,
  qualifying player-palette pixels are nevertheless present.
- Other absent-but-unresolved cases are separately counted.
- **False vetoes over visible players** are counted, never silently folded
  into success.
- Visible but unresolved, unknown and unreviewed examples have distinct
  counts. `UNKNOWN` and `UNREVIEWED` are **not converted to negative labels**.

An opt-in `Argus.Evaluation.Tool --occlusion-veto` call additionally writes
`player-shadow-hard-negatives.json/md` and a locally stored
`player-shadow-feature-rows.csv` alongside the original M2.11 reports.

Each CSV row includes `run_id` (MANDATORY group key), step, raw shadow
status, current player-palette pixel count, RGB deviation, local gradient
fraction, existing veto status and **an independent human-reviewed binary
target only if known**. Unknown/unreviewed targets are blank. CSV floats use
InvariantCulture and string values are escaped. The original screenshots and
manually marked positions are not exported into this CSV.

**This is NOT a trained model**. These are feature rows ready for a subsequent
small, reproducible *offline* classifier study, with whole runs kept together.
A future learner MUST choose its parameters with strictly run-separated
training/validation and freeze them before using independent untouched
held-out data. Multiple frames from the same run are highly correlated.

No changes to active ARGUS game inputs, navigation, player detection,
threat detection, WyrmForge source, hidden state or model training.
The original M2.4 `run-01` held-out has never been opened for tuning.
Owner-private `labels.json` and real original screenshot bytes never enter
this public repository or GitHub CI.

## End-to-end falsification fixture

`tools/m212/generate_hard_negative_fixture.py` constructs a fake but
fully valid **21-frame 390x844 RGB PNG + JSONL** development run.
Its states are explicitly artificial:

| Human reference in artificial screenshot | Raw memory guesses | Current-pixel veto | Unresolved |
| --- | ---: | ---: | ---: |
| Player visible but color shape merged/fragmented | 2 | 0 | 2 |
| Player absent, uniform dark background | 2 | 2 | 0 |
| Player absent, red textured scenery | 2 | 0 | 2 |
| Player absent, blue palette-matching oversized decoy | 2 | 0 | 2 |
| **Total** | **8** | **2** | **6** |

This proves the original empty-patch veto leaves **4 out of 6
human-ABSENT false bridge guesses** unresolved when artificial backgrounds
are harder, even though it rejects both simple blank scenes. Synthetic
failure cases are deliberately hand-designed; do not quote their ratios as
real game error rates, train on them alone or claim opponent recognition.

Four deterministic C# MSTests cover explicit negative strata, run-level CSV
group keys, nullable human targets and inconsistent/corrupt reports. A
full actual .NET 10 CI run builds the original screenshots, calls the real
offline CLI, validates JSON/Markdown/CSV and the **unresolved** failures.
The original bounded external WyrmForge smoke must also pass before merge.

## First trainable-intelligence milestone after this

The next slice is intentionally different from more hand-coded color
rules: create a **first small offline trainable appearance baseline** using
exclusively actual reviewer-supplied development-run data. Suggested
algorithm: class-weighted ridge/logistic linear model or another simple,
auditable low-dimensional model with deterministic initialization.
Establish an untrained baseline and true leave-one-run-out evaluation.
Reject learning if there are too few independent runs, weak human-ABSENT
reference coverage, or if an improvement only works on one procedural
scene. Record false vetoes and unresolved absences per run.

The learned model must remain strictly in a reproducible offline analysis
package. A pass on synthetics or dev-c/d/e is NOT authorization to
steer gameplay, and a new untouched held-out cohort is needed before
any eventual live change.

## Private evaluation command

On a trusted workstation with .NET 10 SDK and **already available**
owner-reviewed original run files:

```bash
dotnet run --project src/Argus.Evaluation.Tool -- \
  --run PRIVATE_M27_ROOT/dev-d --occlusion-veto examples/wyrmforge.profile.json
```

Inspect the three extra files per run and do not combine frame rows from
different development sessions without preserving their run identities.
The assistant runtime's local environment does not currently have an
accessible .NET 10 executable; the C# CLI is tested in public CI on
artificial frames, not against private owner truth.
