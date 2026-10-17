# ARGUS M2.9 — Why a visible player temporarily disappears from the pixel detector

## Research source and boundaries

This is a **development-only** diagnostic prompted by genuine owner-reviewed
2026-10-17 dev-c/dev-d/dev-e screenshots. The original owner reference
files, image bytes and exact coordinates remain private; none should be
added to public GitHub, Actions, or tests. The M2.4 run-01 held-out
remains untouched.

The prior M2.8 report identified **22 human-marked threat centers
without comparable saved combat measurements** in four selected
screenshots. The *reasons differ* and must not be lumped together:

- **dev-c frame 30 and dev-e frame 31** occurred in **transition**
  immediately after selecting a magic upgrade. A `transition` phase
  deliberately does not attempt combat observation. This does not
  demonstrate a failed color classifier.
- **dev-d frame 18** occurred during active **exploring**, surrounded by
  successful player detections in frames 17 and 19. The player-colored
  region expands to roughly **60×60** with a magical outline/nearby
  matching pixels (independent RGB connected-component probe). The
  production color scanner rejects components wider than 52 pixels.
  This explains a plausible geometry rejection without asserting
  game-entity identity.
- **dev-e frame 39** occurred during active **exploring**, bracketed by
  player observations in frames 38 and 40. The largest nearby palette
  component has roughly **51 matching pixels**, fewer than the required
  minimum of 70, with a short fragmented shape. This is consistent
  with a temporarily occluded or recolored player, not proof of absence.

The above component examples are **independent offline pixel-analysis
approximations** of the checked-in original 5×5-adjacency algorithm.
Do not present them as official .NET results on the owner's exact data
until the C# CLI is rerun locally.

## Implementation

The current production `CombatPixelPerception.Locate` continues to
apply **exactly the same thresholds, shape restrictions and candidate
ordering**. M2.9 adds an optional *offline-only* diagnostic callback
to capture rejected player-palette connected components, using the
same real RGB scanner rather than a second, divergent implementation.

`CombatPixelPerception.InspectPlayerColorComponentsRgb` accepts a
**human-reviewed original pixel coordinate** solely as a post-hoc
reference to find nearby component geometry. This is crucial: it
does not infer a player, follow game state, provide a fallback location,
unlock a new enemy identity, or feed any active agent steering.
It exposes reason codes such as `TOO_WIDE`,
`BELOW_MINIMUM_PIXELS`, `TOO_SHORT`, or
`ELIGIBLE_GEOMETRY`, plus number of palette pixels, bounding
rectangle and proximity to the human mark.

New `Argus.Evaluation.PlayerVisibilityAudit` separates
`NOT_COMBAT_PHASE`, `UNKNOWN_REFERENCE`,
`LEGACY_FINAL_UNVERIFIED`, `HUMAN_PLAYER_ABSENT`,
`RECORDED_PLAYER` and `PLAYER_NOT_RECORDED`.
Only for positively human-marked **exploring** screenshots does it
decode a PNG, locate its nearest original color component and
report why the component might not have been accepted.

**A geometrically eligible blob does not guarantee the real tracker
should select it**: competition from other components and history
can still matter. The new report is for human examination, not an
automatic suggestion to lower thresholds. Any player fallback must
undergo separate prospective false-positive controls and a fresh
untouched held-out study before touching live input.

## Offline private reproduction

On a trusted machine with the .NET 10 SDK, keep each verified
`labels.json` with its complete original run PNGs and `trace.jsonl`:

```bash
dotnet run --project src/Argus.Evaluation.Tool -- \
  --run PRIVATE_M27_ROOT/dev-d --player-visibility examples/wyrmforge.profile.json

dotnet run --project src/Argus.Evaluation.Tool -- \
  --run PRIVATE_M27_ROOT/dev-e --player-visibility examples/wyrmforge.profile.json
```

Inspect `player-visibility-audit.json` and
`player-visibility-audit.md`, especially steps **18** and **39**.
Compare the original recorded reason, pixel-component geometry and
neighboring screenshots. Never use the old held-out `run-01` to
select a new threshold. For other runs C/D/E, generate reports
without modifying original human annotations.

## CI gates

Four synthetic MSTests prove one accepted player component, a rejected
overwide effect, a rejected too-small fragment and an empty scene.
A separate end-to-end .NET CLI test reads **five generated synthetic
390×844 PNGs**, original-style JSONL plus artificial human references,
and asserts two losses with the appropriate reason codes, and that
a visible reference on a menu frame is not classified as a combat loss.
No user's private screenshot/label content is committed or uploaded.

After success, run the unchanged bounded external black-box WyrmForge
smoke and require the old perception tests to remain green.
