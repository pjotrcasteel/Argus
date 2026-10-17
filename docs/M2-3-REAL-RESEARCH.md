# M2.3 — Opt-in real two-session research

M2.2 already computes descriptive run-level shadow-persona differences, but was only end-to-end verified on **synthetic multi-run fixtures**. This optional workflow supplies the missing research input: **two separate real WyrmForge browser processes** with bounded screenshot-only observations.

## Launch

Go to the GitHub repository's **Actions** tab → **M2.3 opt-in two-session WyrmForge research** → **Run workflow** on `main`.

The workflow is **not scheduled** and normally does **not run for unrelated PRs**. It also runs once for PR changes to its own workflow file, so it can be validated before first merge. ARGUS requires only free .NET, Playwright Chromium, local OCR, Python and standard GitHub-hosted runners; runner minutes remain subject to GitHub's actual pricing/quotas. There are no paid AI APIs or mandatory infrastructure.

## Each research invocation

1. Restore/build the `.NET 10` solution and install local Chromium/Tesseract.
2. Start browser process #1, with viewport 390×844, max 42 steps and 140-second budget.
3. Close that process and start independent browser process #2 using the same fixed configuration. **Neither receives hidden game state.**
4. Store each run's `trace.jsonl`, `summary.json`, numbered screenshots and `run-manifest.json` in a distinct folder.
5. Store each run's optional `experiment.json` with an *actual* ARGUS SHA, profile-file hash and viewport. The public WyrmForge **version is unknown**, deliberately written as `null`; no version or procedural seed is invented.
6. Run `Argus.Persona.Cohort.Tool` on both actual traces. Duplicated trace bytes cause a hard error. Successful output: `persona-cohort.json`, `persona-cohort.md`.
7. Publish all evidence, even on an unsuccessful experiment, as a **2-day-retention** GitHub Actions artifact.

The exact workflows use limited non-privileged ordinary screenshot and keyboard/mouse input. No WyrmForge repository change or ARGUS source coupling occurs.

## Research limitations

- Two new browser processes provide distinct execution sessions, but cannot prove unique procedural worlds or equal difficulty.
- An original ARGUS smoke gate may be `false`; it is recorded as such rather than being silently replaced or upgraded into a victory claim.
- A malformed/incomplete trace, or identical trace bytes, causes the cohort stage to fail. Artifacts remain available for inspection; it must not silently fabricate a missing run.
- No win/loss or human performance labels are inferred. All `actualOutcome` values stay `unknown`.
- Missing WyrmForge version means **matched context is false**, even if the known profile/viewport/ARGUS SHA match across runs. This is an intentional methodological guard.
- Pairwise direction/action rates are reported **per run**. Multi-frame results inside a run are not independent observations and must not be used for fake confidence intervals.
- No personality shadow proposals are actually executed. Future AI learning, game balancing claims and real persona control still require independently labeled held-out screenshots and validated game outcomes.

## Next step

Inspect real `persona-cohort.md` from a successful workflow; compare per-run unknown evidence and movement differences. Collect more independent runs only when useful. Use the existing M1.9 blind-label UI to create a small, human-reviewed held-out dataset. Longer term, pursue local read-only [Live Spectator Mode](LIVE-SPECTATOR-DESIGN.md) and verified learning policies, without game-side integrations.
