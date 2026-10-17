# M1.6 — Local read-only ARGUS Replay

## Purpose

Inspect what an independent ARGUS agent actually saw and did during a **previously recorded** run. This is the first user-visible visualization step toward the future [Live Spectator Mode](LIVE-SPECTATOR-DESIGN.md), but **it does not stream a live session**.

ARGUS reads its own trace and PNG screenshots. No connection to WyrmForge, a game engine, account, browser automation, paid API, server or database is necessary to view it.

## How to use

A typical ARGUS run writes `trace.jsonl`, `summary.json` and numbered screenshots:

```text
results/my-run/
  frame-0000.png
  frame-0001.png
  ...
  trace.jsonl
  summary.json
```

Run this offline command:

```bash
dotnet run --project src/Argus.Replay.Tool -- --run results/my-run
```

Open `results/my-run/replay.html` in your local browser.

You can play/pause at a deliberate slideshow cadence, seek, use arrow keys to step between frames, and inspect phase, action, reason, agent-reported confidence and the inferred player marker when present.

## Safety and honesty

- The generated HTML is **read-only**. It cannot control a running game and makes no network calls.
- Source data is validated: consecutive step numbering, exact `frame-NNNN.png` names, existing PNG signatures, frame sizes and local disk bounds. Replays stay inside the selected run folder; an arbitrary path in JSON cannot load an outside image.
- Explanations are assigned to DOM `textContent`, never inserted as HTML. Serialized JSON uses default .NET escaping of HTML/script-sensitive characters.
- Image dimensions/markers are derived from **recorded observations**, not game internals. Missing player evidence hides the marker. A marker does **not** establish an accurate enemy identity or successful dodge.
- Maximum 2,000 frames, 12 MiB per screenshot, 350 MiB for all screenshots, 32 MiB trace. The generator does not embed screenshot data in HTML.
- `replay.html` and screenshots remain local, ignored by Git. Do not publish private gameplay or user data.
- Incomplete/garbled traces fail with an explanation; no fabricated intermediate frames or simulated actions.

## Next

First verify the viewer against screenshots from a real completed ARGUS browser run. Then allow opt-in annotation of misclassified player/threats, and only after visual validity improves introduce a **bounded localhost streaming mode**. Live spectator remains tracked in [issue #11](https://github.com/pjotrcasteel/Argus/issues/11).
