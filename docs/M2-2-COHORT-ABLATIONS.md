# M2.2 — Multi-run shadow cohorts and controlled trait ablations

ARGUS is a free, independent black-box visual playtester. **Nothing in this milestone sends alternative actions to a game** or looks at a game's internal state. All outputs are hypotheses from already recorded pixel evidence.

## 1. Compare independent recorded sessions

Prepare sibling folders with **separately executed, real game runs** (not copies of a single recording):

```text
results/experiments/
  session-001/
    trace.jsonl
    frame-0000.png
    ...
    experiment.json (optional)
  session-002/
    trace.jsonl
    frame-0000.png
    ...
    experiment.json (optional)
```

```bash
dotnet run --project src/Argus.Persona.Cohort.Tool -- --runs results/experiments --movement-keys w,a,s,d --seed 42
```

**Outputs (inside `results/experiments`):** `persona-cohort.json` and `persona-cohort.md`, including per-run M2.1 contrast metrics, median/range/unweighted-mean of *eligible per-run* divergence rates, and one-trait-at-a-time effects. Inputs are bounded to 2–128 run folders, 2,000 frames/run by the existing replay reader, and the CLI rejects exact duplicate trace byte contents and linked run folders.

An optional `experiment.json` beside each trace may declare verified, user-provided `gameVersion`, `gameProfile`, `viewport` and `policyVersion`, e.g.:

```json
{
  "gameVersion": "verified-release",
  "gameProfile": "visual-mobile",
  "viewport": "390x844",
  "policyVersion": "argus-git-sha"
}
```

The report flags the context as matched **only** when all four values are present and identical in every run. This is not proof of independent experiences; distinct trace hashes only rule out byte-identical duplicates. Missing or different values mean **descriptive only**.

## 2. Control a single personality trait at a time

M2.0 profiles derive seeded exploration from `persona.Id`, so comparing renamed profiles would have changed apparent exploration even if all actual traits were identical. M2.2 adds an **optional `randomStreamKey`** to `PersonaShadowPolicy.Propose`. The ordinary M2.0 path still defaults to the existing persona ID and thus behaves as before.

For attribution experiments, `PersonaTraitAblations` holds the reference personality, recorded screenshots and **random stream** fixed, and varies exactly one trait between 0 and 1. It separately measures:

- Action-kind differences, such as wait versus move.
- Direction differences, **only when both variants move**.
- Same-key movement with a different hold duration, which is not direction divergence.
- Any full-action differences (including timing).
- Unknown threat-location coverage and valid denominators.

The five controls are risk appetite, exploration, patience, attention and motor precision. A trait having **zero observed effect** can mean that the particular gameplay situation was never encountered. This is a behavior *policy ablation*, not a causal trial involving real players or game outcomes.

For one recorded run, the existing command now additionally creates `persona-ablation.json`:

```bash
dotnet run --project src/Argus.Persona.Tool -- --run results/my-run --movement-keys w,a,s,d --seed 42
```

## 3. Interpreting the results responsibly

- The **unit of aggregation is an entire run**. A run with 200 frames does not outweigh four separate runs with 50 frames each. Do not treat adjacent frames as independent samples for confidence intervals.
- Report run coverage: gameplay frames, unknown candidate positions, available player/confidence evidence.
- `null`/N/A means no valid denominator, not an observed 0% disagreement.
- The reports do not claim that candidate sprites are enemies, that a dodge succeeded, that a personality resembles a real human or that a theoretical alternative action would have changed a game's result.
- Keep the heuristic baseline and seed fixed; a change in policies, captured game version or viewports must be reported.
- For true recognition accuracy and meaningful balancing, collect [M1.9 independently human-reviewed blind labels](M1-9-BLIND-STUDY.md) from unseen held-out runs before validating or activating a learned policy.
- **All computation is local, free and offline**. CI uses synthetic two-run fixtures for cohort validation and a *single* bounded WyrmForge game run to validate one-run trait diagnostics. Those two evidence sources must not be confused with a real two-session research cohort.

## Next

Record several independent game sessions with consistent provenance; compute genuine per-run distributions and secure independent blind reference labels. Investigate what traits do under genuine risk situations. Only after this should ARGUS progress from shadow counterfactuals to carefully bounded real persona-controlled play. User-requested [Live Spectator Mode](LIVE-SPECTATOR-DESIGN.md) is a separate, future read-only component.
