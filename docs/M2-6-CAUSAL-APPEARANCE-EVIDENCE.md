# M2.6d — New human labels: causal appearance audit and research integrity

## New original-image human development references

The owner annotated **all 12 frozen selected screenshots each** in **two independently recorded M2.6b development browser sessions**, dev-a (41 original frames) and dev-b (43). These human coordinates exist privately in the owner's ChatGPT conversation and local research archive. **Do not add `labels.json`, screenshots, original traces, or personal annotations to this public repository.** The 24 label steps match the frozen development-only preselection; the M2.4 run-01 held-out reference has not been opened or modified.

The owner supplied 99 threat-point annotations across 24 selected frames, but **six were on a noncombat transition screenshot** (dev-a step 27). Correctly exclude those rather than fabricating combat false negatives. In dev-b step 14/20, **player is unknown** while human threat labels are known: score threats normally but treat the player as unknown.

## First crosscheck — independent Python mirror, not official .NET

Using the original recorded `trace.jsonl` threat locations and the same 26px greedy one-to-one matching order as `ThreatMatching.Match` (with unobserved phases treated as unknown):

| Development run | Comparable combat frames | Threat TP | FP | FN | Precision | Recall |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| dev-a | 6 | 23 | 13 | 13 | 63.9% | 63.9% |
| dev-b | 9 | 35 | 2 | 22 | 94.6% | 61.4% |
| **Total pooled candidates** | **15** | **58** | **15** | **35** | **79.5%** | **62.4%** |

Thirteen of 13 **attempted, human-known player** positions matched within 26 pixels. Other sampled frames include menus, transitions and two unknown-player references, and **must not** contribute fake true negatives or player accuracy claims. **26 of the 35 unmatched human threat centers** are outside the original color-segmenter scan geometry (vertical 19%–82%, horizontal 1/30–29/30). This is a geometric association, not full causation.

The first-generation appearance audit evaluated ONLY labeled screenshots and bootstrapped player detection at the viewport center *for each snapshot*. With that missing visual history, it dropped three meaningful edge-player frames (dev-a steps 33/34 and dev-b step 39), although the original player-following agent saw them. This slice fixes that **measurement bias**.

## Causal-only fix

- `VisualAppearanceDescriptors.ObservePng` now accepts the optional **prior player blob measured from the immediately preceding screenshot's pixels** and exposes the pixel-detected player. The original parameter-free call remains supported.
- `AppearanceAudit.AnalyzeAsync` now observes **every consecutive screenshot in trace order**, including those not manually annotated, but performs human-reference scoring **only on frozen selected labeled steps**. Noncombat / not-observed frames reset the prior, as does losing the player. No future frame, manual labels or recorded player predictions are ever used to determine the current player.
- `ShadowEdgeStudy.CompareAsync` already scanned all consecutive frames; it now likewise passes its own previous screenshot's visually observed player to avoid dropping valid edge observations.
- Deterministic MSTests assert independent fallback, safe previous-frame continuation and rejection of a far-away unrelated prior. A full five-PNG C# CLI test checks that a player only recognized with causal history yields a scored human-reference threat candidate at the final edge screenshot.

## Second crosscheck — two-run image scanner mirror, still not .NET

On the **same 15 selected comparable combatframes**, using an independent Python/Numba port of the C# RGB rules, candidate blobs and 26px matching, and **causally processing every prior screenshot**:

| Development run | Baseline TP/FP/FN | Raw shadow TP/FP/FN | Baseline recall | Shadow recall |
| --- | --- | --- | ---: | ---: |
| dev-a | 23/13/13 | 30/16/6 | 63.9% | 83.3% |
| dev-b | 35/2/22 | 43/2/14 | 61.4% | 75.4% |
| **Pooled** | **58/15/35** | **73/18/20** | **62.4%** | **78.5%** |

Pooled precision changes from **79.5% to 80.2%**, but these are not independent trials per frame. The shadow proposal recovered **15 extra correct candidate-to-human matches** at the cost of **three extra false positives** across the two new runs. This is a promising **development-only** observation. It differs from earlier run-02 behavior where a naive shadow expansion introduced far more false positives; do not promote the shadow scanner to live controls solely from these two correlated development captures.

**The official .NET CLI has not been run against the real private human labels** in this environment because there is no .NET SDK/runtime here and the private references must not be uploaded to a public GitHub runner. CI runs the same production .NET code on **artificial PNGs and artificial labels**, not the owner's reviewed dataset.

## Next validation

Run locally (on a machine with .NET 10), with dev-a and dev-b `labels.json` kept next to the original private research screenshots:

```bash
dotnet run --project src/Argus.Study.Quality.Tool -- --runs results/m26b-private
dotnet run --project src/Argus.Evaluation.Tool -- --run results/m26b-private/dev-a --appearance-audit examples/wyrmforge.profile.json
dotnet run --project src/Argus.Evaluation.Tool -- --run results/m26b-private/dev-b --appearance-audit examples/wyrmforge.profile.json
dotnet run --project src/Argus.Evaluation.Tool -- --run results/m26b-private/dev-a --shadow-edges examples/wyrmforge.profile.json
dotnet run --project src/Argus.Evaluation.Tool -- --run results/m26b-private/dev-b --shadow-edges examples/wyrmforge.profile.json
```

Confirm that all 15 actual human-reviewed combat screenshots are scored, compare genuine .NET metrics to the mirror, examine per-run false positives and unrecovered HUD/border threats, and gather a **new untouched held-out** session before any real policy promotion. Keep active gameplay unchanged.
