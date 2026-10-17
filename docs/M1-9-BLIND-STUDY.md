# M1.9 — Blind visual reference study across independent sessions

ARGUS is a separate, free, black-box game tester. This is an **offline research protocol**, not AI training and not validated WyrmForge accuracy.

## Why blind and split by run?

The existing annotator shows ARGUS's predicted player/threats. That can bias the reference labels used to judge those predictions. **Blind mode removes prediction coordinates, reported confidence, actions and decision explanations from the generated page's embedded data**, while preserving the original screenshot. Only human-visible evidence remains.

Adjacent screenshots in one session are heavily correlated, so random frame-level training/test splits leak near-duplicate environments. The study planner assigns **entire independently recorded runs** to development (70% approximately) or held-out evaluation (30% approximately; at least one run each). No agent confidences, game outcome or human labels influence selection.

## Run locally (no hosting, no API bills)

Store **two or more actual independent ARGUS runs** as sibling folders:

```text
results/experiments/
  first-real-run/
    trace.jsonl
    frame-0000.png
    ...
  second-real-run/
    trace.jsonl
    frame-0000.png
    ...
```

Then:

```bash
dotnet run --project src/Argus.Study.Tool -- --runs results/experiments --seed fixed-experiment-1 --samples 12
```

Output:

```text
results/experiments/
  study-plan.json
  first-real-run/
    blind-annotate.html
    blind-labels.template.json
    ...
  second-real-run/
    blind-annotate.html
    blind-labels.template.json
    ...
```

`study-plan.json` records schema version, fixed seed, each run ID, assigned partition and sampled frame steps; no absolute paths or private game files are embedded. The planner accepts 2–128 runs and 1–64 frames per run, uses stable SHA-256-based ordering and picks one random frame from each nonoverlapping temporal bin. This reduces time-window clustering and makes reruns of the same seed reproducible. Exact duplicate `trace.jsonl` input files are rejected as non-independent.

You can also generate a **single-run** blind page (convenient for trying the annotation workflow, **not** a valid two-run study):

```bash
dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --blind-annotate results/my-run/blind-annotate.html
```

## Labeling and scoring

Open each `blind-annotate.html` locally, use the pixel-click tools, and export `labels.json` into the **same run folder**. Every label initially says `unknown`. Never infer absence from uncertainty, and don't use ARGUS's normal replay/trace predictions while labeling.

Then score each run using the existing pixel-label evaluator:

```bash
dotnet run --project src/Argus.Evaluation.Tool -- --run results/experiments/first-real-run --labels results/experiments/first-real-run/labels.json
```

Inspect false positives, false negatives and **unknowns** on the development set. Freeze the scoring radius and any algorithm revisions **before** examining held-out quality. Don't tune hyperparameters or select favorable seeds using held-out results. If you want a second iteration, create new independent held-out sessions.

## Evidence and limitations

- A stable partition **does not establish actual independent gameplay**: launch new browser sessions and document build/profile/viewport and procedural seeds where visible or user-supplied.
- Neither the study planner nor the viewer fabricates "human ground truth." The generated templates contain unknown states only.
- The deterministic unit tests use synthetic traces and prove partitioning/integrity, **not detection quality**.
- The live CI can generate a single-run blind viewer, but does not label it or claim a scientifically valid cohort.
- A later milestone should aggregate annotated held-out runs with uncertainty intervals for precision/recall and improve camera-motion classification only on independently reviewed data.
- All screenshots, labels and study plans remain under Git-ignored `results/` or short-lived Actions artifacts. No cloud account, game source access, paid inference or forced data sharing.

## Research gate

Before implementing self-learning policies: gather real reference labels across varied unseen worlds, score independently with unknown coverage and establish a frozen heuristic baseline. Only then compare a learned visual policy against it. Live Spectator Mode remains a separate future goal ([issue #11](https://github.com/pjotrcasteel/Argus/issues/11)).
