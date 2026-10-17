# M2.10 — Conservative shadow-only player memory (offline research)

## Actual problem and scope

Owner-reviewed M2.7 screenshot-only WyrmForge evidence includes two `exploring` frames
where a human sees the player but the current color-based tracker reports no player:
**dev-d step 18** and **dev-e step 39**. The preceding and following frames have
measured actor pixels. In contrast, dev-c step 30 and dev-e step 31 are noncombat
transitions after upgrades, and skipping a combat observation there is correct.
[Previous geometry diagnosis](M2-9-PLAYER-OCCLUSION-AUDIT.md) is separate.

This is a **purely speculative offline shadow research layer**. It does not
change WyrmForge, ARGUS's active movement, camera logic, navigation, detector
thresholds, targeting, threat scanner, real pixel-tracked position or agent
perception. No inferred position is passed back as `previousPlayer` on
an absent frame; the live player remains UNKNOWN.

## Hypothesis — a bounded, visibly uncertain bridge

`PlayerOcclusionShadowTracker` keeps the last **genuinely detected RGB player**
after a pair of compatible consecutive visual detections. Only while game phase
remains `exploring` and no player is found, it emits a *static*
hypothesis based on that last measured position for **at most two consecutive
screenshots**:

| Gap age | Status | Arbitrary research confidence |
| --- | --- | ---: |
| 1 missing frame | `BRIDGE_ONE` | 0.35 |
| 2 missing frames | `BRIDGE_TWO` | 0.15 |
| 3+ missing frames | `BRIDGE_EXPIRED` | 0 |

The confidence values are **not probabilities calibrated against human
ground truth**. They are conservative labels to stop downstream callers from
mistaking a stale location for a fresh image measurement.

Hard guards: no bridge after only one observed frame; any menu/transition and
any third consecutive missing frame erase the prior; a returning measured
component more than 55 px away or more than twice/less than half the previous
bounding-box size resets the continuity streak. Near reappearance is reported
as `REAPPEARED_NEAR_PRIOR`, not proved player identity, and cannot arm another
bridge until a further uninterrupted visually observed frame.

**Still unsolved:** stationary scenery or a color-identical decoy at the
expected pixel coordinate may look like the player in the underlying detector.
This tracker has no independent way to prove actor identity, predict movement
during invisibility or know the real player has left the game. It is unsafe to
use for any game input without a *new* discriminative visual test and independent
held-out outcomes.

## Official .NET offline image study

`PlayerOcclusionShadowStudy` reads **every original consecutively recorded
PNG** and trace phase. For a gameplay screenshot, it invokes the unchanged
`CombatPixelPerception.Observe`, providing only the real player from the
**immediately preceding successful image** as detector history — never the
speculative bridge. A fully measured current screenshot can arm a proposed
bridge in the shadow tracker, but a synthetic virtual player cannot bootstrap
the detector in later missing frames.

Human annotations are loaded **only after** pixel inference. For each
presampled human-reviewed bridge frame, the report distinguishes:

- `BridgeNearVisibleHuman`: speculative stale pixel coordinate within
  the externally chosen 26-px reference radius of a human-marked visible player.
- `BridgeFarFromVisibleHuman`: proposal exists but is too far away.
- `BridgeOverAbsentHuman`: a plainly incorrect candidate proposed over a
  human-reviewed *absent* player. This is a **safety warning**, not a success.
- `BridgeUnknownHumanReference` / `UnreviewedBridgeFrames`: no right to
  claim a good or bad detector outcome without a human reference.

No data splits or threshold choices are selected on the old
`run-01` held-out. All owner `labels.json`, actual screenshot bytes and
private run reports stay outside the public GitHub repository.

On a private .NET 10 workstation, run against only the original owner-reviewed
development data:

```bash
dotnet run --project src/Argus.Evaluation.Tool -- --run PRIVATE_M27_ROOT/dev-d --occlusion-shadow examples/wyrmforge.profile.json
dotnet run --project src/Argus.Evaluation.Tool -- --run PRIVATE_M27_ROOT/dev-e --occlusion-shadow examples/wyrmforge.profile.json
```

Each produces `player-occlusion-shadow.json` and
`player-occlusion-shadow.md` locally. Confirm recorded steps, preselected
human reference coverage and the original `phase` evidence before interpreting
the score. There is **no official .NET result on owner-private screenshots**
yet; CI uses exclusively artificial generated PNG images.

## Deterministic regression and explicit counterexample

Seven pure C# MSTests cover two-frame time-to-live, unarmed first sight,
phase reset, unexpected far/large decoys, reappearance without rearming, and
the fundamental absent-player risk.

An **11-frame synthetic full PNG/JSONL/.NET CLI test** includes two gaps
where a human confirms the player is visible (potentially useful hypotheses)
and two consecutive frames where a human confirms the player is truly
absent (incorrect hypotheses). The research result is required to expose
**two human-absent false bridge proposals**, rather than hiding them.
This is a *counterexample*, not proof that the tracker can detect human
absence. No classifier training or active control activation is proposed.

## Next decision boundary

Run official C# studies with the private owner-reviewed C/D/E development
screenshots; compare the exact missing frames and count absent-player traps.
Consider only separately measurable **current-frame visual evidence**
(e.g. distinct center appearance or motion/segmentation shape) before
contemplating even a shadow confidence classifier. Evaluate a frozen
hypothesis on **new independent untouched held-out sessions**.
The original `run-01` set stays unopened and nothing is automatically
rolled into live steering.
