# M1.8 — Local Annotation Studio

ARGUS now generates a **separate, offline, interactive annotation HTML page** beside the existing read-only replay. The page is only for labeling reference screenshots, never for controlling a game or training a model.

## Use

1. Record a WyrmForge (or any permitted game) session using the existing ARGUS runner.
2. Generate the annotation page:

```bash
dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --annotate results/my-run/annotate.html
```

3. Open `results/my-run/annotate.html` locally. It selects up to 16 genuine captured frames (preferring combat).
4. For each frame, choose **Mark player** and click on the visible player center; or click **Player not visible** / **Can't determine**.
5. Choose **Mark threat** and click each visible hostile/hazardous object you can confidently identify. If none, click **No visible threats**. If occluded or uncertain, click **Can't determine**.
6. Export `labels.json` using the green button, place the downloaded file in `results/my-run`, and run:

```bash
dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --labels results/my-run/labels.json
```

ARGUS produces `perception-report.md` and `perception-report.json`, with player and threat TP/FP/FN/TN and precision/recall where mathematically defined.

## What is guaranteed

- The page can show the original screenshot and a **dashed predicted player point** from ARGUS alongside the independently clicked human label.
- Red markers identify human-labeled threats. Annotation can be undone or reset to known-none / unknown.
- Screens are sampled from actual saved frames, never synthesized or auto-labeled.
- Unknown defaults stay unknown until explicitly changed. Missing recorded predictions are not counted as correct detection.
- Everything stays local: no CDN, WebSocket, server, external script or backend request. Export uses a local browser-generated JSON download.
- Only validated screenshots and trace fields are embedded as escaped JSON. Explanations use `textContent`, not `innerHTML`.

## Limits

- The UI currently uses only up to 16 sampled frames per run and **does not import an already edited labels.json**. Annotate a subset in one session before export.
- It is not the future **Live Spectator Mode**. The live request remains in issue #11.
- Source code, unit tests, and CI generation verify structural correctness. Real browser usability and annotation accuracy require a human review; do not claim human UX validation from a green build.
- Color-segmented threat candidates remain approximate and may include particles or decoration. A few hand-labeled frames are not a reliable cross-seed generalization estimate.

## Next

Review some varied screenshots manually; then improve replay/label navigation, visual-detection confidence and, finally, held-out evaluation with multiple genuinely independent runs. Keep training and test sets separate.
