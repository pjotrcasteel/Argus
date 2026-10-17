# ARGUS Architecture — independent visual agents

## Trust boundary

Real game window → visible pixels → perception → memory/personality/policy → normal player input → real game window.

Never reference the game assemblies or inspect its JavaScript, DOM, save data, network traffic, state, debug API or engine. Do not use WyrmForge Balance Lab/RunSimulation as ARGUS evidence.

## Modules

| Module | Input | Output | Plan |
| --- | --- | --- | --- |
| Game Adapter | Game session | Viewport pixels and ordinary input | Browser adapter now; touch/desktop later |
| Perception | PNG frame | Uncertain HUD/world features | Pixel/CV first, local vision models later |
| Memory | Past observations/actions | Beliefs and episodic memory | JSONL/SQLite |
| Behavior | Observation and personality | Next permitted player action | Heuristics then learned policies |
| Training | Offline episodes | Candidate learned policy | Local, open source |
| Evaluation | Held-out episodes | Controlled outcomes | Repeated black-box runs |
| Research | Traces/screenshots | Balance/UX hypotheses | Local reports/dashboard |

Perception must report uncertainty, e.g. `healthBarFraction=0.3, confidence=0.61`, not engine-truth `HP=32`.

## Personality independent of ability

Risk appetite, patience, motor skill, attention, strategy horizon, exploration and learning rate vary independently. A novice gambler and a veteran gambler should behave differently.

## Scientific validity

- Separate training from held-out evaluation; freeze agent weights during evaluation.
- Match game version, device/resolution, agent profiles and relevant scenario conditions before comparing builds.
- Retain bounded screenshot evidence of relevant decisions and incidents, locally by default.
- Distinguish scripted baselines, heuristic agents and trained policies.
- Never mistake synthetic success for human fun or machine-detected confusion for real human UX confusion.

## Multi-game extension points

`IVisualGameEnvironment` is a transport-independent boundary for observation/actions, but its current `OpenAsync(Uri)` is browser-focused. Desktop launch later requires an explicit launch descriptor or factory—not a fake URL. Game-specific profiles and screen recognizers never belong in `Argus.Core`. See [GAME_ADAPTERS.md](GAME_ADAPTERS.md).

## Current M1 scope

A scaffold, black-box Playwright driver, screenshot capture, JSONL trace and scripted keyboard/mouse replay. **No autonomous navigation, perception model, personalities, learning or dashboard yet.**
