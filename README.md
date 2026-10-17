# ARGUS — Autonomous Research & Gameplay Understanding System

A **standalone, game-independent visual agent research platform**. ARGUS treats games as black boxes: it sees viewport pixels and sends the same mouse/keyboard/controller actions a player could. No game code changes, DOM state, JavaScript injection or hidden engine information.

## Resume this project in a new chat

Say **"continue with ARGUS https://github.com/pjotrcasteel/Argus"**. A new agent should read [AGENTS.md](AGENTS.md) and the [current engineering handover](docs/CONTINUITY.md) first, then check actual GitHub main/PRs/Actions before continuing. The handover includes current hypotheses, measured evidence, outstanding questions and the next implementation step.

## Status

**M1 visual-agent prototype, not a trained AI.** The real screenshot → local OCR → visually located menu click → experimental movement → explainable local trace pipeline is implemented. It **has been exercised against the public WyrmForge browser game** with visual player tracking, simple candidate avoidance, upgrades and per-run evidence; these remain heuristic and not trained. Genuine self-learning, human-calibrated personality behavior and a research dashboard remain future work. See the up-to-date [continuity handover](docs/CONTINUITY.md).

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

## Blind held-out perception study (M1.9)

Use a collection of **independent** saved ARGUS runs. The study tool assigns whole sessions to development or held-out partitions using a fixed seed and generates prediction-free review pages:

```bash
dotnet run --project src/Argus.Study.Tool -- --runs results/experiments --seed fixed-experiment-1 --samples 12
```

For just one existing run, generate a blind review page with `Argus.Evaluation.Tool --blind-annotate`. **No labels are inferred, and no real detection accuracy is claimed yet.** See [M1.9 blind study protocol](docs/M1-9-BLIND-STUDY.md).

## Explore independent player personalities (M2.0 shadow mode)

Run three illustrative personality hypotheses against **the exact same saved visual evidence**, without affecting the real game:

```bash
dotnet run --project src/Argus.Persona.Tool -- --run results/my-run --movement-keys w,a,s,d --seed 42
```

Creates local `persona-shadow.jsonl`, `persona-contrast.json` and `persona-contrast.md` with conservative, adventurous and risk-taking action proposals and measured differences. **These are neither trained AI players nor actual player inputs**. See [M2.0 persona shadow research](docs/M2-0-PERSONA-SHADOW.md) and [M2.1 quantitative comparisons](docs/M2-1-PERSONA-CONTRAST.md).

## Compare independent sessions and isolate individual traits (M2.2)

```bash
dotnet run --project src/Argus.Persona.Cohort.Tool -- --runs results/experiments --movement-keys w,a,s,d --seed 42
```

This produces `persona-cohort.json` and `persona-cohort.md` with one entry per genuinely recorded session, descriptive run-level distributions, unknown evidence and five controlled 0-versus-1 trait ablations. The existing single-run persona tool also writes `persona-ablation.json`. **None of the hypothetical persona decisions is sent to the game.** See [M2.2 cohort and attribution guide](docs/M2-2-COHORT-ABLATIONS.md).

## Capture two real independent browser sessions (M2.3, opt-in)

Open **GitHub Actions → M2.3 opt-in two-session WyrmForge research → Run workflow** to record two separate bounded game sessions and generate real multi-run shadow-persona reports. This does **not** run on every code change. The artifacts are retained for only two days and cannot prove identical procedural conditions. See [M2.3 real research capture](docs/M2-3-REAL-RESEARCH.md).

## Evaluate actual blind labels from independent runs (M2.4)

The opt-in two-session WyrmForge research workflow now includes fixed-seed blind annotation pages and a strictly **NOT_EVALUATED** result until somebody independently reviews screenshots. To validate completed `labels.json` files:

```bash
dotnet run --project src/Argus.Study.Quality.Tool -- --runs results/research-runs
```

This refuses modified frame selections and reports per-run, development and held-out screen-space precision/recall and unknown coverage when genuinely reviewed labels exist. It never invents reference truth or changes the game's code. See [M2.4 quality gate](docs/M2-4-BLIND-QUALITY.md).

## Capture fresh development-only WyrmForge sessions (M2.6b)

Run the opt-in [M2.6b GitHub Actions capture](.github/workflows/m2-6-development.yml) to record
**two separate development sessions**, freeze 12 original screenshots from each, and produce two
prediction-free mobile review pages. This deliberately does **not** access or reassign the
existing held-out run. Two-day private research and mobile-page artifacts are produced, but
the screenshots are not automatically published to the public Pages website.

After manually reviewed `labels.json` files are supplied for both runs, use the original
offline study scorer and M2.6a appearance-audit CLI. Until then the new study is
`NOT_EVALUATED`. Read the [M2.6b protocol](docs/M2-6-NEW-DEVELOPMENT-STUDY.md).

## Architecture and research

- [Architecture and independent visual-only boundary](docs/ARCHITECTURE.md)
- [Multi-game adapters, permissions and desktop roadmap](docs/GAME_ADAPTERS.md)
- [Scene-relative movement evidence](docs/M1-4-SCENE-MOTION.md)
- [Offline multi-run cohort reports](docs/M1-5-COHORT-REPORTS.md)
- [Local session replay (M1.6)](docs/M1-6-LOCAL-REPLAY.md)
- [Human-labeled perception quality (M1.7)](docs/M1-7-PERCEPTION-EVALUATION.md)
- [Local annotation studio (M1.8)](docs/M1-8-ANNOTATION-STUDIO.md)
- [Blind independent-run perception study (M1.9)](docs/M1-9-BLIND-STUDY.md)
- [Personality proposals in shadow mode (M2.0)](docs/M2-0-PERSONA-SHADOW.md)
- [Measuring persona action differences (M2.1)](docs/M2-1-PERSONA-CONTRAST.md)
- [Multi-run personas and one-trait attribution (M2.2)](docs/M2-2-COHORT-ABLATIONS.md)
- [Opt-in real two-session WyrmForge research (M2.3)](docs/M2-3-REAL-RESEARCH.md)
- [Actual blind human-label research and held-out quality (M2.4)](docs/M2-4-BLIND-QUALITY.md)
- [Future zero-cost Live Spectator Mode](docs/LIVE-SPECTATOR-DESIGN.md)
- [Milestones](docs/ROADMAP.md)
- [Cost policy](docs/COSTS.md)

WyrmForge is the first controlled target. Enshrouded or other desktop games require new desktop adapters and explicit permission where applicable.

License not chosen yet.
