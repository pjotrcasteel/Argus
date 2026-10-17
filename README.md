# ARGUS — Autonomous Research & Gameplay Understanding System

A **standalone, game-independent visual agent research platform**. ARGUS treats games as black boxes: it sees viewport pixels and sends the same mouse/keyboard/controller actions a player could. No game code changes, DOM state, JavaScript injection or hidden engine information.

## Resume this project in a new chat

Say **"continue with ARGUS https://github.com/pjotrcasteel/Argus"**. A new agent should read [AGENTS.md](AGENTS.md) and the [current engineering handover](docs/CONTINUITY.md) first, then check actual GitHub main/PRs/Actions before continuing. The handover includes current hypotheses, measured evidence, outstanding questions and the next implementation step.

## Status

**M1 visual-agent prototype, not a trained AI.** The real screenshot → local OCR → visually located menu click → experimental movement → explainable local trace pipeline is implemented. It **has been exercised against the public WyrmForge browser game** with visual player tracking, simple candidate avoidance, upgrades and per-run evidence; these remain heuristic and not trained. Learning, personality vectors and a research dashboard are still future work. See the up-to-date [continuity handover](docs/CONTINUITY.md).

## Repository projects

| Project | Responsibility |
| --- | --- |
| Argus.Core | Game-neutral frames, actions, OCR observations, profiles and evidence contracts |
| Argus.Adapters.Browser | Playwright Chromium screenshot/input transport + free local Tesseract OCR |
| Argus.Agents.Visual | Menu-text recognition and experimental screen-motion exploration heuristics |
| Argus.Runner | Bounded run CLI with visible action trace and recorded PNGs |
| Argus.Tests | Deterministic, source-independent tests for OCR parsing and visual decisions |

## Getting started

Requires **.NET 10**, Playwright Chromium and **Tesseract OCR** installed locally (only visual mode).

    dotnet restore Argus.slnx
    dotnet build Argus.slnx
    pwsh src/Argus.Runner/bin/Debug/net10.0/playwright.ps1 install chromium

### Observe only (default, no input)

    dotnet run --project src/Argus.Runner -- --url https://pjotrcasteel.github.io/Wyrmforge/

### First autonomous visual policy (experimental)

    dotnet run --project src/Argus.Runner -- --url https://pjotrcasteel.github.io/Wyrmforge/ --agent visual --profile examples/wyrmforge.profile.json --max-steps 60 --timeout-seconds 180 --output results/visual

### Scripted transport baseline (not AI)

    dotnet run --project src/Argus.Runner -- --url https://pjotrcasteel.github.io/Wyrmforge/ --agent script --script examples/actions.example.json

All screenshots and JSONL decisions are kept in local ignored results/ directories. No required cloud API, telemetry service or paid AI model. See [M1 visual baseline](docs/M1-VISUAL-BASELINE.md) for limitations and setup.

## Compare multiple independent runs (local)

Use the new offline `Argus.Reporter` to compare folders containing real `summary.json` files:

```bash
dotnet run --project src/Argus.Reporter -- --runs results/experiments --output results/cohorts/demo
```

Reports include coverage and uncertainty, never invented win rates. See [M1.5 cohort analysis](docs/M1-5-COHORT-REPORTS.md).

## Watch a recorded ARGUS run (offline)

```bash
dotnet run --project src/Argus.Replay.Tool -- --run results/your-run
```

Open `results/your-run/replay.html` in your local browser to replay screenshot decisions with a timeline. This is **not live streaming**; see [M1.6 local replay](docs/M1-6-LOCAL-REPLAY.md).

## Evaluate visual detections against your own screen annotations

Generate an annotation template from a recorded run, fill it from the visible screenshots, then score actual recorded decisions. **No game-state access or paid AI is required.**

```bash
dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --template results/my-run/labels.template.json
dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --labels results/my-run/labels.json
```

See [M1.7: perception ground truth](docs/M1-7-PERCEPTION-EVALUATION.md). The template is **not automatically labeled**.

## Label screenshots without manually editing JSON (offline)

```bash
dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --annotate results/my-run/annotate.html
```

Open `annotate.html` to click the real mage and visual threats, explicitly mark unknown/absent, export `labels.json`, and later re-import that file to resume labeling for the existing offline scorer. No account, internet, game source access or cloud required. See [local annotation guide](docs/M1-8-ANNOTATION-STUDIO.md).

## Architecture and research

- [Architecture and independent visual-only boundary](docs/ARCHITECTURE.md)
- [Multi-game adapters, permissions and desktop roadmap](docs/GAME_ADAPTERS.md)
- [Scene-relative movement evidence](docs/M1-4-SCENE-MOTION.md)
- [Offline multi-run cohort reports](docs/M1-5-COHORT-REPORTS.md)
- [Local session replay (M1.6)](docs/M1-6-LOCAL-REPLAY.md)
- [Human-labeled perception quality (M1.7)](docs/M1-7-PERCEPTION-EVALUATION.md)
- [Local annotation studio (M1.8)](docs/M1-8-ANNOTATION-STUDIO.md)
- [Future zero-cost Live Spectator Mode](docs/LIVE-SPECTATOR-DESIGN.md)
- [Milestones](docs/ROADMAP.md)
- [Cost policy](docs/COSTS.md)

WyrmForge is the first controlled target. Enshrouded or other desktop games require new desktop adapters and explicit permission where applicable.

License not chosen yet.
