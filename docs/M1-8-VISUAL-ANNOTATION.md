# M1.8 — Clickable offline screenshot annotation

This milestone makes the existing M1.7 perception evaluator usable without typing dozens of pixel coordinates by hand. It does **not** train ARGUS, integrate with WyrmForge, or stream a live game.

## Start with a recorded run

```bash
dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --annotate results/my-run/annotate.html
```

Open `annotate.html` from the same folder as `trace.jsonl` and the screenshot PNG files in your local browser. The generated page is self-contained and makes no network requests.

The page samples up to 16 actual frames and initializes every player and threat label to **unknown**. Use *Mark player*, then tap/click the visible mage's center; use *Mark threat* to mark visible hostile actors or dangerous projectiles. Click again for each threat. You can explicitly choose *Player absent*, *No threats visible*, or *Can't tell*. Use **Download labels.json** to export the results, and **Import labels.json** to resume editing an exported file. The page does **not** autosave; download before closing.

The UI draws **blue dashed circles for ARGUS predictions** and **green/orange circles for your human labels**. An ARGUS prediction is a hypothesis, not a ground-truth label. Inspect the visible screenshot instead of copying the prediction automatically.

To score the completed human labels:

```bash
dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --labels results/my-run/labels.json
```

The downloaded file must be placed in the recorded run folder. M1.7 validates the exported schema, sampling step IDs, image dimensions and coordinates. Reports are saved as `perception-report.json` and `perception-report.md`.

## Important constraints

- Only recorded, locally stored, user-selected screenshots and original decision traces are used. ARGUS continues to play WyrmForge independently.
- No browser backend, cloud account, API key, paid service or WyrmForge source integration.
- The viewer uses only relative image dimensions to translate touch/mouse clicks into original viewport pixel coordinates.
- Unknown isn't absence: ambiguous or occluded objects stay unknown. Explicitly verified empty threats is a known empty list.
- Labeling only starts when a human actively clicks. The generated file and all predictions remain **unverified** until someone assesses the screenshots.
- The UI validates imported labels for run ID, sampled steps, player state and coordinate bounds. The .NET evaluator independently validates all uploaded/local labels before computing statistics.
- Files are exported by the browser's download action. If downloaded elsewhere, move `labels.json` into the local run folder before scoring.
- The GitHub Actions workflow generates a short-lived **unannotated** `annotate.html` from a public WyrmForge run. It never manufactures ground truth.

## Next research gate

Independently annotate a diverse set of actual screenshots from different world seeds and game versions. Compare precision, recall and false positives against *held-out* frames. Only then should we change the combat policy based on candidate velocities or start local machine-learning experiments.

The future [Live Spectator Mode](LIVE-SPECTATOR-DESIGN.md) remains a separate requested feature; this is offline only.
