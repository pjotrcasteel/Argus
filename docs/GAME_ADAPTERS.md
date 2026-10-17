# Multi-game adapters: vision and input only

ARGUS is a game-independent research platform, **not** a WyrmForge-integrated test library. The AI kernel only receives visible pixels and sends inputs ordinary players can produce.

## Boundaries

- `Argus.Core`: observations, actions and policies; no game package or browser dependency.
- `Argus.Adapters.Browser`: Playwright screenshots and ordinary browser input only. No DOM/JS/storage/internal network inspection.
- Future `Argus.Adapters.Desktop`: OS screen capture and permitted keyboard/mouse/controller actions. **Not implemented.**
- Future game profiles: permitted launch details, visible-controls examples and training scenarios—no privileged game state.

## Adding a new game

1. Check owner/platform permission for automation; never bypass anti-cheat, DRM or access controls.
2. Verify screenshot scale and coordinates in a real session.
3. Make input safety limits, stop control and local action logs mandatory.
4. Keep training data separate from held-out evaluation and disable learning for evaluation.
5. Validate UX hypotheses with human testers.

## 3D-game example

Enshrouded would need a desktop adapter, complex 3D perception and significantly more local computation. ARGUS has no Enshrouded support or authorization today. WyrmForge is our controlled first game.

## Evolving the interface

`IVisualGameEnvironment.OpenAsync(Uri)` currently covers URLs; introduce a launch descriptor/game-session factory for desktop executables without leaking OS process details into the decision and perception layers.
