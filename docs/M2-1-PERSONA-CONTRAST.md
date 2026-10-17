# M2.1 — Measuring shadow persona differences without claiming human realism

This milestone turns M2.0's qualitative *conservative*, *adventurer* and *daredevil* action proposals into reproducible, quantitative evidence.

## Generate a report from a recorded external play session

```bash
dotnet run --project src/Argus.Persona.Tool -- --run results/my-run --movement-keys w,a,s,d --seed 42
```

All output stays in the ignored `results/my-run` folder:

- `persona-shadow.jsonl`: existing frame-by-frame, non-executed action proposals from all profiles.
- `persona-contrast.json`: new machine-readable pairwise and per-persona statistics.
- `persona-contrast.md`: new readable report including uncertainty and denominators.

The same screenshots, source observations, seed and movement-key mapping are used for all profiles. Original gameplay stays controlled by the **unaltered baseline**; this work does not inspect game internals or send any alternative inputs.

## Crucial metric definitions

| Metric | Denominator and interpretation |
| --- | --- |
| Proposed frames | Count of frames in which this profile suggests an action (including Wait). |
| Wait / Move | Distinguishes hesitation from directed movement; never counts NoAction as Wait. |
| Different action kinds | Frames where *both* profiles suggest an action, but one proposes Wait and the other Move (or another kind). |
| Different movement directions | **Only** frames where both profiles propose a move; movement keys are compared, case-insensitively. |
| Same direction, different duration | Both move using the same key but propose different held-key durations. **Not** counted as a direction disagreement. |
| Any action difference | Frames where both propose an action and their full actions differ (kind, key, duration, coordinates). |
| Agent confidence available | Source trace contained a visual-agent confidence estimate for this gameplay frame; it is *not* calibrated probability of actual correctness. |
| Threat candidate coverage | Source trace recorded a list of candidate pixel centers. Missing (`null`) and known-empty (`[]`) are distinct. |

Every percentage uses the specific applicable denominator; if no eligible frames exist, the rate is `null` in JSON / `N/A` in Markdown, **never fake 0%**. Pairwise counts show true direction disagreement separately from timing or action-kind differences.

## Limits

- **No label validation:** image-derived threats may be particles or decorations; a difference in proposals is a reproducible policy difference, not a survival advantage.
- **No proven realism:** three designed trait presets are behavioral hypotheses, not user modeling, machine learning or psychological measurement.
- **No full-game extrapolation:** all alternatives see frames from one run executed by the baseline. The next actual screenshot after an alternative input is **unknown**; we cannot score counterfactual outcomes.
- **No independent replication from one run:** a 50-frame report is one correlated gameplay session, not 50 independent players or successes.
- **No changes to gameplay:** only the existing game-neutral offline persona library, CLI, tests, documentation and existing public CI artifact are updated.

## Next research actions

1. Run or retrieve **at least two genuinely independent browser play sessions**, fix the persona seed and version, compare the descriptive per-run results and refrain from treating adjacent frames as independent trials.
2. Continue [M1.9 blinded human labeling](M1-9-BLIND-STUDY.md) on independent held-out screenshots to quantify perceptual false positives and unknown coverage.
3. Once visual quality and observable outcomes are reliable, add a limited real persona-control experiment with a hard stop and repeated comparable conditions.
4. Only thereafter consider local learning, reward design and model selection. A free/local [Live Spectator Mode](LIVE-SPECTATOR-DESIGN.md) remains planned separately.
