# ARGUS — Autonomous Research & Gameplay Understanding System

**An independent, black-box AI playtesting research platform.** No WyrmForge source dependency, game instrumentation, privileged game state, DOM reading, engine hooks, or game modifications.

> Status: **M1 starter scaffold**. The runner can open a web game, capture visible pixels, and send scripted mouse/keyboard actions. It is **not yet** a self-learning AI or a complete game-playing agent.

## Goal

ARGUS should eventually control games the way players do: perceive the screen, act by ordinary player inputs, remember past play, learn strategies, simulate human behavior profiles, and produce evidence-based balance and UX insights. WyrmForge is the first target, but ARGUS must remain reusable.

## Architecture

- `Argus.Core`: shared contracts for visible observations, actions, agents, and game environments.
- `Argus.Adapters.Browser`: external Playwright screenshot/input adapter with no game-specific code.
- `Argus.Runner`: scripted observation/replay loop; later supports other adapters and autonomous policies.
- `docs/ARCHITECTURE.md`: separation boundaries, agent evolution and experimental validity.
- `docs/GAME_ADAPTERS.md`: future desktop/3D game adapters and responsible usage.
- `docs/ROADMAP.md`: milestones from Eyes & Hands through autonomous learning.
- `docs/COSTS.md`: free-by-default operating model.

## Prerequisites

- .NET 10 SDK
- Chromium installed using Playwright's browser installer
- Local hardware for longer experiments (no required cloud subscription or paid API)

## First smoke test

```bash
dotnet restore Argus.slnx
dotnet build Argus.slnx
# Install Chromium using the adapter's generated Playwright installer:
pwsh src/Argus.Adapters.Browser/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet run --project src/Argus.Runner -- --url https://pjotrcasteel.github.io/Wyrmforge/ --output results/wyrmforge-observe
```

Observe-only mode opens the page and saves one PNG plus JSONL evidence; it **does not claim to play**. To smoke-test mouse/keyboard commands, use the harmless example script:

```bash
dotnet run --project src/Argus.Runner -- --url https://pjotrcasteel.github.io/Wyrmforge/ --script examples/actions.example.json --output results/wyrmforge-input
```

The sample script only waits and moves the mouse pointer; it never guesses the game's UI coordinates. All outputs stay local in ignored `results/`.

## Principles

1. **External black box:** observations are viewport pixels; actions are ordinary input. No DOM, localStorage, JS injection, source introspection, game events, or internal APIs.
2. **Personality is not skill:** reaction delay, risk tolerance, attention and learning ability remain separate.
3. **Never fake learning:** scripted actions are smoke tests, not AI.
4. **Evidence first:** independent training/evaluation, screenshot traces and comparable cohorts.
5. **UX humility:** synthetic confusion generates UX hypotheses; human trials remain necessary.
6. **Free by default:** local training, no mandatory commercial API or cloud GPU.
7. **No game-code changes:** ARGUS only changes in this repository; respect each game's automation rules.

## License

No license has been selected yet. Public source code does not automatically grant permission for reuse.
