# M2.0 — Distinct player-personality hypotheses (shadow mode)

ARGUS's goal is to support **different human-like playing styles**: careful, adventurous, risk-seeking, impatient, observant and so on. These are *design hypotheses*, **not verified simulations of real people and not learned models**.

## Shipped prototype

Game-neutral traits, each bounded from 0 to 1:

| Trait | Initial behavioral hypothesis |
| --- | --- |
| RiskAppetite | Retreat from a nearby visual threat *candidate* versus approach it |
| Exploration | Prefer a stable direction versus varied seeded directions |
| Patience | Sometimes stop and gather more observations instead of pushing forward |
| Attention | Wait when confidence in the inferred player position is too low |
| MotorPrecision | Simulate shorter reactive key holds versus longer coarse input |

Initial illustrative presets: `conservative`, `adventurer`, `daredevil`.

The proposals only occur in previously recorded `exploring` frames. Other phases produce **no hypothetical gameplay action**, not a guess at menus or upgrades. No proposal is sent to a game or changes ARGUS's existing baseline behavior. Unknown threats and unverified visual sprites are *not treated as ground truth*.

## Run on a recorded session

```bash
dotnet run --project src/Argus.Persona.Tool -- --run results/my-run --movement-keys w,a,s,d --seed 42
```

Supply exactly four allowed movement keys in `up,left,down,right` order matching the **external game adapter's** normal input mapping. These are not WyrmForge-specific class dependencies.

Writes `persona-shadow.jsonl` **beside** the recorded screenshots and `trace.jsonl`. Every frame contains the same captured observation, the baseline action label and all three alternative proposals with explanations and visual confidence. Outputs are local and Git-ignored under `results/`. They contain no real game outcomes, fitness/rewards or hidden game state.

## What can be tested now

- **Determinism:** same evidence + personality vector + numeric seed generates the same proposal.
- **Meaningful contrast:** for a visible nearby candidate, a low-risk shadow persona retreats while a high-risk one approaches.
- **Risk gating:** no shadow navigation when no visible player or insufficient confidence, or outside gameplay.
- **Actions are hypotheses:** different proposals do not imply better performance or realism. Current enemy candidates can include scenery and effects; the gameplay itself remains controlled by the frozen visual baseline.
- **Free:** no model serving, provider subscription, trained neural net, cloud database or integration with WyrmForge.

## Next prerequisites

1. Collect independent browser game sessions and **blind human-labeled screenshots** using [M1.9](M1-9-BLIND-STUDY.md). Establish perception accuracy on held-out runs before allowing persona alternatives to control gameplay.
2. Define measurable behavioral targets (risky movement into verified hazards, distance maintained, exploration breadth, hesitation) without inventing inner player traits.
3. Then move selected personas from shadow proposals to deliberately **bounded** controllable runs with fail-safe stop conditions, and compare against the same game conditions.
4. Only when observations/rewards are trustworthy, try local learnable policies with versioned datasets and a frozen heuristic baseline; evaluate out-of-sample.
5. User's future Live Spectator Mode remains [issue #11](https://github.com/pjotrcasteel/Argus/issues/11), not implemented as part of this milestone.
