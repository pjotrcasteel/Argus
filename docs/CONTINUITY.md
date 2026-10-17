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

## Current implementation (as of main after PR #13)

- `Argus.Core`: screenshots, actions, agent/profile interfaces, game-neutral evidence and session summary.
- `Argus.Adapters.Browser`: black-box Playwright screenshots and ordinary keyboard/mouse; local Tesseract OCR with bounded per-frame timeout.
- `Argus.Agents.Visual`: OCR-driven menu/magic-card clicks; visually identified circular route nodes; optional button fill detection; screen-pixel mage palette tracking with temporal prior; uncertain candidate-threat segmentation and simple evasive policy; separate **diagnostic-only** camera/background translation and scene-relative candidate-motion estimator.
- `Argus.Runner`: bounded single-session CLI; PNG screenshots, JSONL actions + reasons, `summary.json` evidence report; optional `--require-combat-vision` gate.
- `Argus.Analysis` + `Argus.Reporter`: **offline-only** cohort reader and reporting CLI. Read multiple independent `summary.json` runs, validate integrity, optionally compare controlled-manifest metadata, and produce local `cohort.json` / `cohort.md`. Wilson 95% interval is for visual smoke-gate frequency only. See [M1.5 cohort reports](M1-5-COHORT-REPORTS.md).
- `Argus.Replay` + `Argus.Replay.Tool`: validate saved `trace.jsonl` and numbered PNG screenshots, and generate local `replay.html` with play/pause, timeline seek, frame steps, inferred player position, actions and decision reasons. No server/integration/streaming; see [M1.6 replay](M1-6-LOCAL-REPLAY.md).
- `Argus.Tests`: deterministic MSTest cases for visual UI, geometry, player tracking, steering, motion, evidence, cohorts and secure local replay.
- `.github/workflows/ci.yml`: .NET 10 build/tests. `m1-live-smoke.yml`: *bounded* browser WyrmForge run; collects short-lived evidence; requires evidence gate.
- Already observed by the **external** player: open the public WyrmForge page, begin a hunt, select Arcane Orb, choose a route and enter the trail, move, choose upgrades and sometimes recognize an end screen. These are heuristic actions, NOT AI training.

Recent merged work: [PR #8 combat vision](https://github.com/pjotrcasteel/Argus/pull/8), [PR #9 evidence acceptance gate](https://github.com/pjotrcasteel/Argus/pull/9), [PR #10 camera-aware motion evidence](https://github.com/pjotrcasteel/Argus/pull/10), [PR #12 offline cohort reports](https://github.com/pjotrcasteel/Argus/pull/12), [PR #13 local replay](https://github.com/pjotrcasteel/Argus/pull/13).

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

## Current engineering judgement / active hypotheses

1. **Perception is the primary bottleneck, not learning algorithms yet.** Stabilize player identity and player-vs-effects-vs-scenery classification before training policies or doing statistical balancing. Image colors alone are fragile.
2. **Camera motion contaminates raw screen displacement.** Current M1.4 scene-shift is diagnostic only because particles, parallax, zoom and effects make it uncertain. Review against a labeled/held-out sample before allowing it to affect action selection.
3. **Static decorations can resemble enemies.** Candidate labels are intentionally uncertain. Multi-frame association and temporal consistency should reduce false positives, but require measurement.
4. **The first evidence gate is too weak for balancing.** Menu/route clicks + player frames can succeed without a meaningful win/loss. The M1.5 reporter addresses data integrity, denominators and comparability, but does NOT infer victory. Collect labeled outcomes, run manifests and held-out independent runs before using any metric to adjust balance.
5. **Replay before realtime streaming.** The user wants to watch one ARGUS run in real time later. The safest sequence is comparable run data → local read-only post-run replay → bounded localhost spectator (2–5 fps), always independent of game input and with no paid platform.
6. **Avoid hidden coupling to WyrmForge.** The first game-specific visible labels/palettes live in a profile, not in the core. New games get adapters/profiles, not integrations.
7. **The generated public game can change between runs.** Recheck on the real website; multiple procedural seeds and screen sizes matter. One screenshot or one lucky run is not evidence of robust play.

## Single recommended next slice

**M1.7: evidence labeling and perception calibration** before ML training or activating camera-relative enemy tracking as an action policy.

- Use the new [offline replay viewer](M1-6-LOCAL-REPLAY.md) to review a small, diverse set of WyrmForge screenshots and annotate whether player, threat, decoration, particle and background-shift detections were plausible. Keep labels **local**; no WyrmForge internals or DOM.
- Add a validated local annotation/ground-truth format and deterministic offline scoring against annotated frames. Include unknown/occluded categories; false positives must be counted, not discarded.
- Specifically diagnose camera/background-shift coverage: a prior run produced 8 estimates and another 0. Consider multiple resolutions, spells, procedural seeds and whether the screen actually pans between frames.
- Do not make confident claims about balancing or self-learning based solely on visual candidates. The M1.5 reporter is *descriptive* without independently labeled outcomes and comparable run manifests.
- Once perception has acceptable out-of-sample evidence: gather independent runs with genuine game-version/profile/viewport/policy manifests and visual outcome labels; eventually make personality profiles and genuine learning experiments.
- **Future user-requested goal:** Live Spectator Mode, [issue #11](https://github.com/pjotrcasteel/Argus/issues/11), showing a **real-time single run** with explanations. Reuse the tested offline viewer architecture, but local stream is **not implemented** and should follow evidence validation.

## Local usage

```bash
dotnet restore Argus.slnx
dotnet build Argus.slnx
dotnet test tests/Argus.Tests/Argus.Tests.csproj
dotnet run --project src/Argus.Reporter -- --runs results/experiments --output results/cohorts/demo
dotnet run --project src/Argus.Replay.Tool -- --run results/local-run
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
- **2026-10-17 — M1.6 completed:** [PR #13](https://github.com/pjotrcasteel/Argus/pull/13) merged: safe local offline replay generator, 48/48 tests and real game-generated `replay.html` in run 39024084743. **Next: M1.7 annotated perception quality**, then cautiously revisit local live spectator [issue #11](https://github.com/pjotrcasteel/Argus/issues/11). No live streaming yet.
