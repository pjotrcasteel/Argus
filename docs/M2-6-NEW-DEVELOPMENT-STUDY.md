# ARGUS M2.6b — New independent development captures and blind mobile review

This milestone adds a **NEW two-session DEVELOPMENT cohort**. It deliberately does not inspect, relabel,
publish or tune against the original M2.4 `run-01` held-out screenshots. The previous real M2.4
`run-02` labels continue to serve only as earlier development evidence.

## Capture and fixed selection

The opt-in [M2.6b workflow](../.github/workflows/m2-6-development.yml) starts two **separate**
bounded, external public WyrmForge browser processes (`dev-a` and `dev-b`), each at 390×844,
42 maximum steps and 140-second runtime. Browser interaction is strictly screenshot + ordinary input;
there is no game code, DOM, privileged API or game-state connection.

Each run saves a manifest containing UTC timestamps, actual runner exit code, profile checksum,
ARGUS commit SHA, viewport, screenshot count, trace SHA-256 and UNKNOWN game version/outcome.
Two traces with the same SHA cannot count as independent sessions. Independence from the procedural
world RNG is **not** asserted, only independent browser processes and distinct recorded traces.

The deterministic, predeclared seed `argus-m26b-development-frozen-20261017` selects twelve
screenshots from non-overlapping temporal bins in **each** run using
`Argus.Study.Tool --development-only`. The `study-plan.json` explicitly includes
`mode: development-only` and both run assignments must be `partition: development`.
The original M2.4 partitioned planner and old plans remain backward compatible. Missing, duplicated,
unsafe or reordered runs and modified sample selections are rejected by study validation.

`Argus.Study.Quality.Tool` must declare **NOT_EVALUATED** until a human supplies the independent
labels. Empty/all-unknown annotations cannot generate precision/recall.

## Mobile review package (not automatically public)

The workflow prepares two independent Safari-compatible pages at
`annoteren/dev-a/` and `annoteren/dev-b/`, using the existing iPhone-verified visual
annotation UI and **only the 24 preselected real PNG screenshots**. Predicted player/enemy
coordinates, agent action, reasoning and trace metadata are never included in the rendered
HTML. The template begins with all states **unknown**, not absent.

The workflow verifies these pages with actual mobile Chromium: image loads, navigation, tapped
player and threat positions, JSON download and persistence on reload. It uploads separate
**two-day GitHub Actions artifacts**:
`argus-m26b-independent-development-evidence` (original research traces, fixed plan and
blank labels) and `argus-m26b-mobile-blind-review` (only selected images and mobile pages).

The owner **explicitly authorized publishing the 24 new public-game screenshots** on
2026-10-17, while preserving the original run-02 route. [M2.6c Pages workflow](../.github/workflows/mobile-annotator-pages.yml)
combines the previous verified development screenshot page with the two existing pre-reviewed,
prediction-free mobile artifact pages, without including raw traces, hidden predictions, held-out
screenshots or human labels.

Deployment must be confirmed before claiming these three HTTPS routes work:
`https://pjotrcasteel.github.io/Argus/annoteren/dev-a/`,
`https://pjotrcasteel.github.io/Argus/annoteren/dev-b/`, and unchanged
`https://pjotrcasteel.github.io/Argus/annoteren/run-02/`.
They are **public GitHub Pages screenshots**, but personal annotations remain local to each
device until copied/exported. The user should export separate `labels.json` documents per run.
No public upload endpoint for user-entered labels exists.

The current Actions source archives have two-day retention; deployed Pages serves its static
assets beyond that, but a future redevelopment/rebuild needs renewed archive sources.
Preserve or migrate exact original assets before that retention window expires.

## Workflow

In GitHub Actions, select **M2.6b independent development evidence and mobile review**, then
**Run workflow**. A PR changing that workflow also runs a bounded test capture once, subject to
GitHub Actions policy and minute limits. Download artifacts before the two-day expiry.
Keep copies in a local private work folder and do not commit PNGs, raw traces or imported
`labels.json` files to the repository.

After independent manual annotation on iPhone, export **one `labels.json` per run** and
place each beside its corresponding original `dev-a/trace.jsonl` or `dev-b/trace.jsonl`.
The filenames are the same but the `runId` inside identifies the run; never merge them into
one fake session.

Then reproduce (offline, .NET 10, from repository root):

```bash
dotnet run --project src/Argus.Study.Quality.Tool -- --runs results/m26-development
dotnet run --project src/Argus.Evaluation.Tool -- --run results/m26-development/dev-a --appearance-audit examples/wyrmforge.profile.json
dotnet run --project src/Argus.Evaluation.Tool -- --run results/m26-development/dev-b --appearance-audit examples/wyrmforge.profile.json
```

The human label records must retain exactly the original frozen frame indices. Use
`unknown` for genuinely ambiguous imagery, `known` with an empty array when no
threat is visible, and `absent` only when the player is genuinely absent.

## Scientific and product boundaries

The evaluator does **not** learn yet, and old RGB-based candidate masks are not known enemy
identities. Treat two short development runs as two clusters, never 24 independent games.
Inspect per-run matched/unmatched feature distributions and misses; avoid tuning to the
untouched previous held-out run. Before claiming generalization, freeze a future trained
scorer and acquire a fresh, truly untouched held-out run after all tuning. No active gameplay
steering or WyrmForge source may be altered for this workflow.

## Next step after PR merges

Verify that the two independent screenshot sessions and review packages actually exist in a
successful Actions artifact, then arrange owner-controlled iPhone review (an explicit Pages
publishing opt-in if browser hosting is needed). Wait for real human labels before calculating
human-reference precision, recall or any learned object classification.
