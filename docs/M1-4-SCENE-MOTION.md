# M1.4 — Evidence for scene-relative movement

ARGUS remains an independent black-box player: no game integration or internal state queries.

## Implemented
- Estimate 2D translation of textured background pixels between adjacent screenshots, excluding the central mage and HUD regions.
- Return null for absent or unreliable visual evidence.
- Match candidate silhouettes across frames, subtract the background translation and classify observed candidates as visually moving or stationary.
- Emit camera displacement confidence and matched/moving/stationary counts in the per-frame trace and session summary.
- Add synthetic deterministic tests for camera panning, stationary scenery, moving candidates, untextured frames and uncertain inputs.

## Deliberate limits
- Not the true world-space camera transform. Zoom, transitions, parallax and heavy effects can invalidate the estimate.
- Not semantic enemy identification. Animated hazards, particles and decorative items can match the detector; use the word *candidate*.
- Does not yet influence the agent's movement. Calibrate against annotated real screenshots first, then validate on held-out game versions.
- A positive count does not prove a kill, a successful dodge, player happiness or balanced mechanics.

## Acceptance
- Green .NET build and deterministic tests.
- Bounded live WyrmForge run with motion evidence in JSONL/summary; retain the M1 visual-combat evidence gate.
- Review how often the scene estimator reports a known shift versus unknown, and whether results match screenshots.

## Follow-up
Use multiple independently recorded runs, annotated screenshots and held-out verification before deciding whether candidate motion should drive combat. Reuse the same frame stream for future local Live Spectator Mode.
