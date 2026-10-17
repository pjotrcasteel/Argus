# M2.4 — Independent blind labels and truthful held-out quality

M2.3 captures **two real, separate WyrmForge browser sessions**. M2.4 connects those runs to M1.9's blind screenshot review and the existing M1.7 pixel evaluator. It deliberately **does not generate human annotations or assert unmeasured accuracy**.

## Opt-in real research collection

Go to **GitHub → Actions → M2.3 opt-in two-session WyrmForge research → Run workflow**.

A successful research artifact now includes:

```text
research-runs/
  study-plan.json
  study-quality.json
  study-quality.md
  persona-cohort.json
  run-01/
    trace.jsonl
    frame-0000.png...
    blind-annotate.html
    blind-labels.template.json
  run-02/
    trace.jsonl
    frame-0000.png...
    blind-annotate.html
    blind-labels.template.json
```

The study uses a fixed, documented seed and up to 12 screenshots spread across the entire timeline **per run**, selected before any human assessment. Complete runs (not neighboring frames) are assigned to development versus held-out. Agent-predicted positions, confidence and reasons are omitted from blind annotation data.

**The workflow must initially produce `NOT_EVALUATED`.** It cannot truthfully create precision/recall unless people have reviewed the original screenshots. This is a successful research-instrumentation result, not a detector evaluation.

## Human review (local, free)

1. Download and extract the short-lived research artifact. Open each run's `blind-annotate.html` next to its `frame-####.png` files in a desktop browser.
2. Mark the visible player's center, absent, or unknown, and all visible threats (hostile entities or hazardous projectiles), known-empty or unknown. If a screenshot is ambiguous, choose **unknown**, never zero objects. Don't consult ARGUS's regular replay/predictions while labeling.
3. Click **Export labels.json** and place it in the corresponding run folder as `run-01/labels.json` or `run-02/labels.json`. Import it back into the viewer later if needed.
4. Evaluate locally:

```bash
dotnet run --project src/Argus.Study.Quality.Tool -- --runs results/research-runs
```

The command writes `study-quality.json` and `study-quality.md`, retaining separate development and held-out results. It does **not** upload labels or change the game.

## Safeguards

- The tool **recalculates the entire seed-based partition and selected screenshot indices from each validated trace**. Modified assignments or a cherry-picked subset of annotated frames cause a hard error.
- All selected steps must remain in each imported label file, even if individual object labels remain `unknown`. Unlabeled runs and all-unknown templates produce **`NOT_EVALUATED`**, not invented zeros, perfect scores or false negatives.
- Partial human reviews remain **`PARTIALLY_REVIEWED`**. Fully filled-in label files are `HUMAN_LABELS_PRESENT`, which means only that their states are complete; ARGUS cannot cryptographically prove a human created or accurately reviewed them.
- Uses **recorded, unchanged visual-agent predictions** and bounded 26-pixel screen-space matching (including one-to-one candidate matching). It never re-runs a different version of the detector over historic frames.
- Reports actual per-run player/threat TP/FP/FN/TN and unknowns, plus **per-run median/range** within development and held-out partitions. An undefined rate appears as N/A; no false pooled independence assumption.
- The provided seed must be fixed **before** labels are viewed; recomputation detects incompatible plans but cannot prevent a user deliberately regenerating a valid plan with a *different seed*. Store published research manifests for reproducibility.
- Screenshots within one run are correlated. Two runs are not enough for tight confidence intervals. Camera motion, game version and procedural world difficulty may differ, and the evidence still does not verify encounters won or learning.
- The standard CI tests use **synthetic** coordinates to verify scorer plumbing, unknown semantics, tampered-plan rejection and exact sample membership. CI does not impersonate a human reviewer.
- All tools are free, local-first, black-box and independent of WyrmForge code. The GitHub research action is intentionally opt-in and stores images only as two-day artifacts.

## Next priority

Collect a small independently human-reviewed dataset, examine visually what the player detector mistakes for the mage and what the threat segmentation mistakes for particles or terrain, and measure missing-evidence coverage **by held-out run**. Only then set a transparent quality threshold for moving personality policies from hypothetical shadow decisions to bounded executed gameplay. Learning and Live Spectator Mode remain later milestones.
