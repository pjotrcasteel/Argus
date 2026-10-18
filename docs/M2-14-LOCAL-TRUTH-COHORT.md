# ARGUS M2.14 — Fix the learning target before capturing more development truth

## Critical correction: what is a positive training label?

M2.13 introduced the first actual tiny trainable model, entirely offline, but
its initial M2.12 CSV recorded a **globally visible player** as positive even
when the actor could be far away from the stale guessed position. That is a
**target definition error** for the task: ARGUS must learn whether an earlier
player position is supported by the **current** screenshot.

**M2.14 freezes a LOCAL target with a 26-pixel radius** (the established
M2.10 human–candidate matching criterion):

- Human marked player **VISIBLE** with original, independently observed
  distance to the *prior causal pixel candidate* **<=26 px**: local positive `1`.
- Human marked player **VISIBLE** but **>26 px away**: local NEGATIVE `0`,
  even though a real player appears elsewhere in the same screenshot.
- Human marked player **ABSENT** anywhere: local NEGATIVE `0`.
- Human state **UNKNOWN** or unreviewed: **blank**, never assumed absent.
- Human VISIBLE without a finite independent distance (or absent/unknown
  carrying a distance) is a **data-integrity error**, never guessed.

This is an **offline, post-hoc target only**. Human labels never enter
online screenshot perception, steering or temporal tracker state.
Far-visible cases form a separate, important hard-negative stratum.
A nearby coordinate is not proof of actor identity, but this
precommitted 26px human-reference criterion is an operational
measurement rather than global-presence fiction.

The CSV training-target column changes deliberately from
`reviewed_player_label` to
`reviewed_candidate_local_label_v2`. M2.13's input reader refuses the
previous version's header, so cached globally-positive labels cannot
silently train a falsely optimistic model. Generate new CSVs using
`Argus.Evaluation.Tool --occlusion-veto` **privately** before retrying
the learner.

## New opt-in real development cohort

The workflow `.github/workflows/m2-14-development.yml` is
**manually dispatched**, not automatically run on every PR or merge.
It launches three separate real Chromium screenshot-only black-box
sessions against the **public, unmodified WyrmForge game**: `dev-f`,
`dev-g`, `dev-h`. No WyrmForge source, DOM, internal game engine,
privileged telemetry or paid service access. Viewport **390×844**,
at most **72 runner steps and 190 seconds** per independent session.

This captures development evidence ONLY. The existing `run-01`
held-out, other partitioned studies, and old dev-A/B/C/D/E labels are
not touched, relabeled, mixed into the new capture or republished.

Before any capture or predictions, we preregistered:

| Property | Frozen M2.14 value |
| --- | --- |
| Partition | `development-only` |
| Run IDs | `dev-f`, `dev-g`, `dev-h` |
| Seed | `argus-m214-blind-24-bins-development-only-20261018` |
| Samples | **24 chronological temporal bins per run**, 72 total |
| Frame selection | Original `StudyPlanner.CreateDevelopment`, one SHA256-seeded draw/bin |
| Human truth at capture | All **UNKNOWN**; no generated labels |
| Publication | **Not automatic**; explicit renewed user approval needed for public Pages |
| Evidence | Original PNGs, full JSONL trace, manifest, frozen study plan |
| Browser review | iPhone-sized pages, local-only annotation until user exports |

Frame selection never uses candidate confidence, player guesses,
predicted deaths, successful upgrades, human labels or a post-hoc model
to cherry-pick samples. Trace hashes and capture manifest document
the independent runs. The workflow fails if one session is incomplete
or indistinguishable from another.

### Important training coverage warning

**24 broadly sampled screenshots/run do NOT guarantee 10 labeled
local shadow candidates/run.** A screen can have a player, but not
an M2.10 memory proposal, and a label on it does not magically
become a training row.

After the 24 frame indices are frozen, a separate **all-UNKNOWN**
`--occlusion-shadow` research pass recomputes the actual raw
shadow proposals over the full PNG sequence and reports how many
fall in the already frozen 24 images. This assessment is saved as
`m214-candidate-coverage-preview.json`. It NEVER changes the
sample selection or human labels and is NEVER embedded in the blind
mobile review pages. A high candidate count is not proof of positive
or negative labels; a low count indicates human reviewing the full
24 screenshots might not unlock M2.13's strict readiness gate.

All captures are archived as short-lived GitHub Actions artifacts;
the iPhone-friendly pages are in a **separate ZIP**, not inserted
into the existing ARGUS Pages deployment. Prior public review URLs
for run-02/dev-a/b/c/d/e remain completely untouched.
Only after inspecting authentic captures, coverage, mobile QA and
an explicit **additional** user permission should a future PR
add these 72 new images to public Pages.

## Safety and validation

Synthetic .NET 10/MSTest cases establish deterministic frozen
24-bin selection, all-UNKNOWN labels, and local-positive vs
far-visible-negative vs human-ABSENT-negative classification;
an invalid missing reference is rejected. The standard .NET
CI constructs three distinct 55-frame synthetic runs, invokes
the **actual** Study.Tool, Study.Quality.Tool and
mobile-review packager, and checks 72 blank original screenshots,
JavaScript parsing, run identity, and screenshot-only files.
A manually run M2.14 workflow additionally performs real
mobile-Chromium touch/navigation/export/reload smoke testing on
the independent real capture before marking it ready to review.

A separate standard bounded live WyrmForge visual-agent smoke
must pass unchanged before merging. **No live AI or game policy is
updated, no learned model is activated, and no real model accuracy
or gameplay competence is inferred from synthetic tests.**

## What happens next

First check whether the new frozen cohort has enough
real **candidate-local** proposals for an efficient human review.
Do not ask the owner to manually annotate all 72 images unless
the evidence supports that effort. If candidate scarcity persists,
design a separately preregistered **proposal-level candidate review**
with blinded source pixels and unbiased predeclared eligibility,
rather than retrofitting current screenshots with artificial
negative truth.

After genuine review, run M2.14 feature regeneration and M2.13
`Argus.Learning.Tool` locally with all real human references;
expect `NOT_READY` unless each whole independent development
run satisfies the strict M2.13 positive/negative/hard-negative
sample gate. Do not train on or open original held-out `run-01`
for tuning. A NEW untouched whole-run evaluation is required
before any gameplay integration is even proposed.
