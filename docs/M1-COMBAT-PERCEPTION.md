# M1.2 — External combat perception and experimental avoidance

ARGUS reads **only viewport screenshots**. WyrmForge assemblies, DOM, saved data, in-game telemetry and internal APIs remain off limits.

## New capability

- Identify a possible mage silhouette using a configurable RGB palette and size constraints.
- Identify possible nearby visually distinct threat silhouettes using additional palette rules.
- Record screenshot pixel positions, candidate counts, distance and steering source in JSONL traces.
- Bias movement away from nearby candidate objects; otherwise preserve the pre-existing exploratory baseline.
- Report uncertainty explicitly: colored scenery, effects, lighting and UI may cause false positives.

These are *screen-space observations*, not verified enemy identities, world coordinates, real health values or a learned policy. A player might stay near the center during world-camera scrolling; screen displacement alone cannot prove effective movement.

## Acceptance gates

- Unit tests: distinct synthetic sprites, unknown-player fallback, HUD exclusion and close/distant candidate steering.
- GitHub Actions build and deterministic tests.
- Real, bounded public WyrmForge browser run with screenshot and trace evidence.
- Manually review whether the mage and threat candidates match screenshot pixels; do not claim success from CI alone.
- Preserve a fallback when no recognizable mage can be found.
- Future work: learn/calibrate palettes from independent training frames, classify hazards and enemy motion, estimate world-camera movement, detect kills/defeat and benchmark held-out scenarios.

The first version uses no paid AI service, GPU or subscription. It does not attempt learning yet.
