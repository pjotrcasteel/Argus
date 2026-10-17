# ARGUS M2.5e — Stable stationary candidates: recovery versus decoration (shadow-only)

## Research question

The original M2.5c raw-edge scanner recovered some true human-marked threats but produced extra false positives. M2.5d's moving-only temporal filter removed many false candidates yet lost still or slow real threats. A stationary visual appearance may be either **an enemy** or **terrain / HUD / particles**. No game-internal state is used to decide.

**This slice does not change `VisualGameAgent`, the active `ObserveRgb` detector, or avoidance decisions.**

## Four independent offline score columns

The existing `--shadow-edges` local evaluation now reports:

- Original center-region baseline.
- Raw edge shadow proposals.
- Temporal moving-only proposals (2+ consecutive valid sightings; observed movement at least 3px).
- **Experimental stationary proposal audit:** moving proposals plus *visually stable still* candidates seen for at least 3 consecutive valid frames, less than 3px total motion, with similar bounding-box dimensions and matching pixel count over their entire track. These are NEVER classified as definite enemies.

All candidate history is causal: the evaluator scans every preceding recorded frame, even when human ground-truth for that frame is intentionally unknown. It only scores preselected, explicitly human-reviewed frames, and resets tracks on noncombat/unknown-player transitions. Legacy final screenshots with copied old evidence are excluded. The original private `run-02` labels and screenshots are not committed or uploaded. `run-01` remains untouched.

```bash
dotnet run --project src/Argus.Evaluation.Tool -- --run results/research/run-02 --shadow-edges examples/wyrmforge.profile.json
```

Outputs remain the local `shadow-edge-study.json` and `shadow-edge-study.md` with four-way TP/FP/FN, precision/recall, and per-frame proposal counts.

## Validation

MSTest covers three-sighting stationary eligibility, appearance instability, state resets and distinguishing moving confirmed candidates from still review proposals. The GitHub CI also generates an **entirely synthetic** three-frame RGB game scene containing a moving candidate and a static lookalike. With a human-like synthetic label identifying the still object as a threat, the stationary study improves recall; with exactly the same pixels and a synthetic label marking it as decoration, the fourth column instead accrues a false positive. This is a test of correct measurement, **not an image classification benchmark**.

The full real `run-02` reference is private; independently estimated prior six-frame baseline, raw and moving-temporal results (26/4/11, 30/13/7, 27/5/10 TP/FP/FN respectively) remain approximate, not official .NET results. No fourth-column real precision is asserted without running the official CLI.

## Next gate

Collect additional independent and blind human-reviewed **development** runs before adjusting stationary thresholds; inspect whether moving scenery, camera shifts and particles survive the stability rule. A stationary entity can be a genuine enemy or a decorative object with identical visual properties. Do not promote any of these research candidates to gameplay until an independent held-out test and bounded gameplay trial support it.
