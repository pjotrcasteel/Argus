# M1.7 — Label and score visual perception, without game internals

ARGUS can only infer player positions and potential threats from pixels. Until compared to **human-visible, independently labeled screenshots**, none of those inferences should be treated as reliable game truth.

## New recorded evidence

New ARGUS JSONL traces include a `evidence.combat.threatLocations` list (screen pixel centers of **candidate objects**). Previous traces without this field are supported, but their threat detection accuracy is **unknown**, never silently zero.

## Create a human annotation template

```bash
dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --template results/my-run/labels.template.json
```

The template samples up to 16 frames, favoring combat. **Every value is deliberately `unknown` until a human reviews the screenshots.** No labels are invented.

For each selected frame, inspect `frame-####.png` in the replay viewer, then save `labels.json`:

```json
{
  "schemaVersion": 1,
  "runId": "my-run",
  "frames": [
    {
      "step": 18,
      "player": { "state": "visible", "center": { "x": 195, "y": 420 } },
      "threats": {
        "state": "known",
        "centers": [{ "x": 115, "y": 390 }, { "x": 280, "y": 460 }]
      }
    },
    {
      "step": 23,
      "player": { "state": "unknown", "center": null },
      "threats": { "state": "unknown", "centers": null }
    },
    {
      "step": 30,
      "player": { "state": "absent", "center": null },
      "threats": { "state": "known", "centers": [] }
    }
  ]
}
```

These are **format examples**, not real annotations. A threat may be a hostile actor or hazardous projectile; establish a consistent annotation policy for what counts as a threat before labeling multiple runs. Decorations and nonhazardous spell effects **must not** be labeled as threats, so a detection on them contributes a false positive. If the truth cannot be determined from the visible image, set `unknown` (never `absent` or `known: []`).

## Evaluate

```bash
dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --labels results/my-run/labels.json
```

Output stays inside the local ignored run folder: `perception-report.json` and `perception-report.md`.

The evaluator uses **recorded decisions**, not an improved detector retrospectively run against old images. Each prediction is matched against independent reference centers within a configurable 26-pixel radius; one detection can match at most one labeled object. It reports TP/FP/FN/TN and precision/recall/F1 where defined. A badly placed player location contributes **one FP and one FN**. The threat metric reports how many annotated frames were unscorable because older traces lacked candidate locations.

## Boundaries and experimental validity

- Only local, read-only PNG/JSONL and human-generated annotation JSON; no API, game engine, DOM, save files or hidden state.
- Review frames from multiple procedural seeds and screen sizes. Separate calibration runs from held-out evaluation runs. A high training score is not evidence of transfer.
- These are **screen-space point metrics**, not true world positions, verified enemy identity or success/failure of dodging.
- The labels and reports are never automatically uploaded or committed. The CI workflow writes only a completely **unknown** template from its public, anonymous browser run.
- The current combat avoidance policy is unchanged by this milestone. No learning, model training or live spectator is added.

## Next

Use the generated replay artifact to manually annotate a small, diverse selection of frames. Check false positives and false negatives; then extend evaluation to camera shift and camera-relative tracking with held-out pairs, before training or trusting candidate-based steering.
