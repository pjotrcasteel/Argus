# ARGUS — continuity and engineering handover

> **Read this first when resuming the project.** Repository: https://github.com/pjotrcasteel/Argus
>
> Last inspected and updated: **2026-10-17**. This document describes *publicly shareable engineering state, decisions, hypotheses and next actions*, not private reasoning. It is **not** a substitute for checking the current GitHub `main`, open PRs, CI and fresh evidence.

## One-sentence restart instruction

"Continue with ARGUS: https://github.com/pjotrcasteel/Argus . Read `AGENTS.md` and `docs/CONTINUITY.md` first; check current code, open PRs and actions, then implement the next validated vertical slice. Update the handover before stopping."

## Mission / owner's intent

Create a **game-independent, standalone, free-by-default autonomous visual playtesting laboratory**. Its first game is WyrmForge, being developed separately by another agent. ARGUS must act from screenshots and ordinary player input like a human player and independently evaluate navigation, UX and eventual game balance. Future targets may include permitted desktop games, but only if their terms/permission allow automated play.

- **Never integrate ARGUS into WyrmForge's codebase.** No shared assemblies, DOM introspection, game JS injection, internal endpoints, memory inspection, debug state or privileged telemetry.
- **No mandatory paid APIs, SaaS, cloud GPUs, databases or streaming services.** `.NET 10`, Playwright/Chromium, local Tesseract OCR, MSTest and local PNG/JSONL are the foundation.
- Personalities (risk-taking, patience, skill, attention, exploration etc.) and **genuine self-learning** are future goals; do not imply either is implemented today.
- Work in **small-to-medium tested vertical slices**, use PRs, check GitHub Actions, and inspect *real screenshot/trace evidence* before merging. Prefer correctness to feature count; do not claim a green CI job proves an AI plays competently.
- The owner explicitly wants a **future Live Spectator Mode** where one real run can be watched with visual overlays and its decision explanations. Design: [LIVE-SPECTATOR-DESIGN.md](LIVE-SPECTATOR-DESIGN.md); tracking: [issue #11](https://github.com/pjotrcasteel/Argus/issues/11). **Not implemented yet.**

## Sources of truth

1. Current `main` code, projects and configuration: [repository](https://github.com/pjotrcasteel/Argus).
2. [Open PRs](https://github.com/pjotrcasteel/Argus/pulls) and [GitHub Actions](https://github.com/pjotrcasteel/Argus/actions); never assume an old PR is still open or a historical build describes latest `main`.
3. [Roadmap](ROADMAP.md), [architecture](ARCHITECTURE.md), [game adapters](GAME_ADAPTERS.md), [cost guardrails](COSTS.md), [M1.4 motion evidence](M1-4-SCENE-MOTION.md), [Live Spectator design](LIVE-SPECTATOR-DESIGN.md).
4. CI screenshot/trace artifacts from live WyrmForge runs **expire quickly** (two-day retention). If unavailable, re-run the existing bounded live workflow or run locally; do not invent a past screenshot.

## Current implementation (as of main after PR #15)

- `Argus.Core`: screenshots, actions, agent/profile interfaces, game-neutral evidence and session summary.
- `Argus.Adapters.Browser`: black-box Playwright screenshots and ordinary keyboard/mouse; local Tesseract OCR with bounded per-frame timeout.
- `Argus.Agents.Visual`: OCR-driven menu/magic-card clicks; visually identified circular route nodes; optional button fill detection; screen-pixel mage palette tracking with temporal prior; uncertain candidate-threat segmentation and simple evasive policy; separate **diagnostic-only** camera/background translation and scene-relative candidate-motion estimator.
- `Argus.Runner`: bounded single-session CLI; PNG screenshots, JSONL actions + reasons, `summary.json` evidence report; optional `--require-combat-vision` gate.
- `Argus.Analysis` + `Argus.Reporter`: **offline-only** cohort reader and reporting CLI. Read multiple independent `summary.json` runs, validate integrity, optionally compare controlled-manifest metadata, and produce local `cohort.json` / `cohort.md`. Wilson 95% interval is for visual smoke-gate frequency only. See [M1.5 cohort reports](M1-5-COHORT-REPORTS.md).
- `Argus.Replay` + `Argus.Replay.Tool`: validate saved `trace.jsonl` and numbered PNG screenshots, and generate local `replay.html` with play/pause, timeline seek, frame steps, inferred player position, actions and decision reasons. No server/integration/streaming; see [M1.6 replay](M1-6-LOCAL-REPLAY.md).
- `Argus.Evaluation` + `Argus.Evaluation.Tool`: offline, human-labeled **visible pixel reference evaluation**. Creates all-`unknown` templates, validates player/threat annotations, evaluates the *recorded* locations with TP/FP/FN/TN and nullable precision/recall/F1; old traces with missing threat coordinates remain `unknown`. [M1.7 docs](M1-7-PERCEPTION-EVALUATION.md).
- `Argus.Evaluation.AnnotationPageBuilder`: independent, offline `annotate.html` with clickable player/threat markers, absent/unknown controls, ARGUS predicted-position overlay and local `labels.json` export. Unlike the replay, the **annotation file** is editable, but neither page controls the game. [M1.8 guide](M1-8-ANNOTATION-STUDIO.md).
- `Argus.Tests`: deterministic MSTest tests for visual UI, geometry, tracking, motion, cohorts, replay, perception scoring and HTML/JS safety.
- `.github/workflows/ci.yml`: .NET 10 build/tests. `m1-live-smoke.yml`: *bounded* browser WyrmForge run; collects short-lived evidence; requires evidence gate.
- Already observed by the **external** player: open the public WyrmForge page, begin a hunt, select Arcane Orb, choose a route and enter the trail, move, choose upgrades and sometimes recognize an end screen. These are heuristic actions, NOT AI training.

Recent merged work: [PR #10 motion evidence](https://github.com/pjotrcasteel/Argus/pull/10), [PR #12 offline cohort reports](https://github.com/pjotrcasteel/Argus/pull/12), [PR #13 local replay](https://github.com/pjotrcasteel/Argus/pull/13), [PR #14 perception label scoring](https://github.com/pjotrcasteel/Argus/pull/14), [PR #15 local annotation studio](https://github.com/pjotrcasteel/Argus/pull/15).

## Prior motion evidence experiment (2026-10-17)

[GitHub Actions run 39022431491](https://github.com/pjotrcasteel/Argus/actions/runs/39022431491), on PR #10. Build/unit tests and live workflow passed. Its `summary.json` reported:

| Item | Value |
| --- | ---: |
| Frames observed | 57 |
| Menu clicks | 2 |
| Route clicks | 2 |
| Upgrade clicks | 2 |
| Frames with inferred player | 28 |
| Candidate avoidance frames | 17 |
| Frames missing player while exploring | 13 |
| Estimated background-shift frames | 8 |
| Frames with possible moving candidates | 5 |
| Frames with possible stationary candidates | 1 |
| Terminal stops | 0 |
| Visual-combat evidence gate | PASS |

**Interpretation:** The game was reached and some visible behavior was recorded. Only 8/57 frames got camera-motion estimates. These 5 moving-candidate frames do **not** establish five actual enemies or successful dodges. The existing gate is intentionally a *transport/perception* smoke test, not a gameplay quality, accuracy or balance benchmark. The 0 terminal stops in this run is not a failure of the bounded smoke, but no completed run may be asserted for that example.

## Latest verified GitHub CI and live experiment (2026-10-17)

- [PR #12](https://github.com/pjotrcasteel/Argus/pull/12) was merged into `main` as `db166f08e997a9528d3f785b36054f6a88f7d1c0`.
- [.NET build + 42 deterministic tests](https://github.com/pjotrcasteel/Argus/actions/runs/39023654750) succeeded after fixing an incorrect test fixture and enforcing **missing camera counters ≠ zero**.
- [Live WyrmForge smoke](https://github.com/pjotrcasteel/Argus/actions/runs/39023654745) passed existing gate, independent of the offline reporter: **57 frames, 2 route interactions, 2 upgrades, player seen in 37 frames, 32 candidate-avoidance frames, 5 missing-player exploration frames, 0 terminal stops, 0 camera-motion estimates**.
- **Interpretation:** the experiment reached combat; `0/37` estimated camera shifts in this run is a major reliability issue, not a finding of zero true movement. Do not use current candidate velocity as objective balancing data.
- No actual multi-run real-game cohort report has yet been generated from independent sessions. Reporter correctness is verified using deterministic test fixtures, not a live balancing experiment.

## Latest verified replay milestone (2026-10-17)

- [PR #13](https://github.com/pjotrcasteel/Argus/pull/13) merged on `main` at `c6a42867fd6c42049151a1ce6e66489e508aea58`.
- [.NET CI run 39024084790](https://github.com/pjotrcasteel/Argus/actions/runs/39024084790): successful build and **48/48 tests**.
- [Real WyrmForge run 39024084743](https://github.com/pjotrcasteel/Argus/actions/runs/39024084743): **52 frames**, 34 frames with inferred player, 2 route interactions, 1 visible terminal stop; existing smoke gate passed.
- Crucially, that live workflow **generated `replay.html` from the actual saved PNG + JSONL trace** via `Argus.Replay.Tool`. Its [short-lived artifact](https://github.com/pjotrcasteel/Argus/actions/runs/39024084743/artifacts/12098166006) includes evidence and viewer. Artifact retention is two days, so recreate if unavailable.
- **Scope:** offline HTML replay, not live streaming. The page was generated successfully by CI against real data; interactive display has not been human-reviewed in a local browser and must not be claimed to be UX-validated.

## Latest verified perception-instrumentation milestone (2026-10-17)

- [PR #14](https://github.com/pjotrcasteel/Argus/pull/14) merged to `main`, commit `b27249b372109a125c2b26c54b4a44133ededcd5`.
- [.NET CI run 39025485807](https://github.com/pjotrcasteel/Argus/actions/runs/39025485807): **54/54 deterministic tests passed**, build succeeded.
- [Public WyrmForge run 39025485863](https://github.com/pjotrcasteel/Argus/actions/runs/39025485863) passed browser smoke, 18 frames with an inferred player, 2 route clicks, 1 visible terminal stop, and wrote an all-`unknown` **16-frame annotation template** from the real trace. [Short-lived artifact](https://github.com/pjotrcasteel/Argus/actions/runs/39025485863/artifacts/12098656427).
- **No manually verified ground-truth frames were created yet**. The annotation scorer is tested on synthetic fixtures only. **Do not claim player/enemy precision, recall or quality thresholds yet.** The new trace now records threat candidate screen centers in addition to counts. Some combat frames without a located player still have unavailable threat evidence, not zero detections.

## Latest verified perception instrumentation (2026-10-17)

- [PR #14](https://github.com/pjotrcasteel/Argus/pull/14) merged into `main` at `b27249b372109a125c2b26c54b4a44133ededcd5`.
- [.NET CI run 39025485807](https://github.com/pjotrcasteel/Argus/actions/runs/39025485807): successful build and **54/54 tests**.
- [External WyrmForge smoke run 39025485863](https://github.com/pjotrcasteel/Argus/actions/runs/39025485863): existing visual-combat gate passed; 18 inferred player frames, 2 route clicks, 1 terminal stop. Generated [replay and 16 all-unknown frames for manual annotation](https://github.com/pjotrcasteel/Argus/actions/runs/39025485863/artifacts/12098656427).
- IMPORTANT: no labels were independently verified; no real precision/recall figure exists yet. New tool proves schema, input validation and deterministic scoring, not detector quality.

## Latest verified annotation-UI milestone (2026-10-17)

- [PR #15](https://github.com/pjotrcasteel/Argus/pull/15) merged on `main` at `6bb3660527eca041759bacb6255cfdbd761e913b`.
- [.NET CI run 39025964482](https://github.com/pjotrcasteel/Argus/actions/runs/39025964482): successful build and **58/58 tests**.
- [Live WyrmForge run 39025964485](https://github.com/pjotrcasteel/Argus/actions/runs/39025964485): 57 frames, 35 frames with inferred player, 2 route clicks, 2 upgrade clicks, 7 inferred camera shifts, 0 terminal stops; visual-combat gate PASS. [Short-lived artifact](https://github.com/pjotrcasteel/Argus/actions/runs/39025964485/artifacts/12098717008) contains real screenshots, replay, **16-frame all-unknown template**, and locally generated `annotate.html`.
- Generated JavaScript was checked with `node --check` in CI; no syntax failures. **Real interactive usability on an actual user's browser and manual annotation accuracy have not yet been independently validated.**
- The template and UI **do not make automatic labels**; no true precision/recall exists yet for WyrmForge player/enemy recognition.

## Current engineering judgement / active hypotheses

1. **Perception is the primary bottleneck, not learning algorithms yet.** Stabilize player identity and player-vs-effects-vs-scenery classification before training policies or doing statistical balancing. Image colors alone are fragile.
2. **Camera motion contaminates raw screen displacement.** Current M1.4 scene-shift is diagnostic only because particles, parallax, zoom and effects make it uncertain. Review against a labeled/held-out sample before allowing it to affect action selection.
3. **Static decorations can resemble enemies.** Candidate labels are intentionally uncertain. Multi-frame association and temporal consistency should reduce false positives, but require measurement.
4. **The first evidence gate is too weak for balancing.** Menu/route clicks + player frames can succeed without a meaningful win/loss. The M1.5 reporter addresses data integrity, denominators and comparability, but does NOT infer victory. Collect labeled outcomes, run manifests and held-out independent runs before using any metric to adjust balance.
5. **Replay before realtime streaming.** The user wants to watch one ARGUS run in real time later. The safest sequence is comparable run data → local read-only post-run replay → bounded localhost spectator (2–5 fps), always independent of game input and with no paid platform.
6. **Avoid hidden coupling to WyrmForge.** The first game-specific visible labels/palettes live in a profile, not in the core. New games get adapters/profiles, not integrations.
7. **The generated public game can change between runs.** Recheck on the real website; multiple procedural seeds and screen sizes matter. One screenshot or one lucky run is not evidence of robust play.

## Concurrent pull-request caution (checked 2026-10-17)

- [Open PR #16](https://github.com/pjotrcasteel/Argus/pull/16) proposes **another M1.8 annotation viewer implementation**, built independently on an earlier branch. It overlaps merged [PR #15](https://github.com/pjotrcasteel/Argus/pull/15); GitHub currently marks it **not directly mergeable**.
- It also proposes useful **additional** elements: import/resume labels.json, predicted threat overlays in replay, and a headless Chromium UI smoke test. These must be evaluated selectively with a diff against current `main`.
- **Do not merge or overwrite PR #15 code blindly; avoid a second overlapping annotation implementation.** Prefer a fresh, narrow PR cherry-picking/reimplementing only non-duplicative, tested benefits if the owner still wants them. Preserve other contributors' work.
- PR #16's UI smoke may supply the missing genuine browser-interaction verification. But its tests are not yet validated against current `main`, so do not claim that verification has already passed.

## Single recommended next slice

**M1.9: genuine held-out visual ground-truth study**. The measurement and labeling tools are now built; do not add further combat heuristics before collecting trustworthy reference labels.

1. Download/extract the newest short-lived WyrmForge Actions evidence, open `annotate.html` on a desktop browser, and **test that clicking, navigation and local JSON export actually work**. Leave incorrect UI interactions as bugs to be fixed in a focused PR.
2. Independently label a small, diverse sample of real screenshots (different procedural seeds, combat intensity, colors, effects, viewport sizes). Keep run folders and labels Git-ignored; do not upload private game sessions.
3. Use `Argus.Evaluation.Tool --labels` on those real labels. Calculate TP/FP/FN, and precision/recall with denominators and unknown coverage for player and candidate threats. Keep calibration and held-out evaluation frames/runs strictly separated. If no actual labels exist, report **no real accuracy**.
4. Investigate the observed instability of camera-shift estimates (historical 8 frames, then 0, latest 7). Do not infer motion=false from unknown estimation, and do not feed relative enemy velocity into the action policy until measured.
5. Then prioritize a reliable, inexpensive perception baseline and small cohort studies under explicitly matched game/build/profile metadata. *Only afterward* start personality vectors and genuine self-learning.
6. User wants to watch a real-time run eventually: [Live Spectator issue #11](https://github.com/pjotrcasteel/Argus/issues/11), local-first and free. Existing recorded replay/annotation work is a precursor, not the feature itself.

## Local usage

```bash
dotnet restore Argus.slnx
dotnet build Argus.slnx
dotnet test tests/Argus.Tests/Argus.Tests.csproj
dotnet run --project src/Argus.Reporter -- --runs results/experiments --output results/cohorts/demo
dotnet run --project src/Argus.Replay.Tool -- --run results/local-run
dotnet run --project src/Argus.Evaluation.Tool -- --run results/local-run --template results/local-run/labels.template.json
dotnet run --project src/Argus.Evaluation.Tool -- --run results/local-run --annotate results/local-run/annotate.html
pwsh src/Argus.Runner/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet run --project src/Argus.Runner -- --url https://pjotrcasteel.github.io/Wyrmforge/ --agent visual --profile examples/wyrmforge.profile.json --max-steps 56 --timeout-seconds 180 --output results/local-run
```

The local **visual** mode requires Tesseract installed. Screenshots and traces are Git-ignored. Do not upload or commit personal game sessions or secrets. The public GitHub Actions job operates only on publicly accessible/logged-out WyrmForge and produces short-lived artifacts.

## Continuity protocol — update this after each meaningful working slice

1. Check current `main`, open PRs/issues and latest Actions before choosing changes.
2. Create a focused branch, implement, test, run appropriate bounded live evidence if behavior changes.
3. Record **what changed**, PR/commit/CI links, measured evidence, limitations and exact next action in this file.
4. Update the README/roadmap when milestones change; keep `AGENTS.md` pointing here.
5. Merge only when build/tests and, for behavior changes, real visual evidence support the claim. Leave honest blockers in the PR and here.
6. Before answering the user, check that this file is actually on GitHub **main** and direct the user to its permalink.

### Status log

- **2026-10-17 — Baseline:** after merged PR #10: screenshot-only visual game prototype and motion diagnostics. Live run 39022431491 was green with the numbers above. Next: M1.5 offline multi-run cohort comparison.
- **2026-10-17 — M1.5 completed:** [PR #12](https://github.com/pjotrcasteel/Argus/pull/12) merged: offline validation, Markdown/JSON cohort reporting, controlled-manifest matching and technical-gate uncertainty. Unit tests and live smoke passed; latter had **0** inferred camera shifts.
- **2026-10-17 — M1.6 completed:** [PR #13](https://github.com/pjotrcasteel/Argus/pull/13) merged: safe offline replay generator, 48/48 tests and real `replay.html` from run 39024084743.
- **2026-10-17 — M1.7 instrumented:** [PR #14](https://github.com/pjotrcasteel/Argus/pull/14) merged: human-label schema, scored pixel metrics and actual threat locations in traces; 54/54 tests and a real all-unknown template.
- **2026-10-17 — M1.8 implemented:** [PR #15](https://github.com/pjotrcasteel/Argus/pull/15) merged: offline clickable annotation studio with per-frame labels and local JSON export, 58/58 tests, real WyrmForge HTML generation + JavaScript syntax check. **Not yet tested interactively by a human**; no manually verified reference dataset or WyrmForge precision/recall. **Next:** M1.9 small independent held-out annotation study; local Live Spectator [issue #11](https://github.com/pjotrcasteel/Argus/issues/11) remains future.

- **2026-10-17 — M1.7 instrumented:** [PR #14](https://github.com/pjotrcasteel/Argus/pull/14) merged, 54/54 tests and public game smoke passed; 16 sample frames generated **all unknown**, no measured human-labeled accuracy. Next M1.8 offline point-and-click annotation UI.
