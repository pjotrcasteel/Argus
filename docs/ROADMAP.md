# ARGUS Roadmap

## M1 — Eyes & Hands
- [x] Independent .NET 10 repository scaffold and game-independent contracts.
- [x] External browser screenshot/input adapter in its own project.
- [x] Local PNG and JSONL trace format.
- [x] .NET build and deterministic tests green on GitHub.
- [ ] Manually verify WyrmForge browser session.
- [ ] Detect visible screen regions and UI with confidence scores.
- [ ] Prototype genuine touch input for mobile.
- [x] External pixels-only menu, upgrade and route interactions validated on live WyrmForge.
- [x] Agent recognizes a visible terminal screen and stops without privileged game state.
- [x] Visual mage tracking and candidate avoidance baseline, with confidence/evidence traces.
- [ ] M1.4 validate camera-relative candidate motion on multiple game runs and held-out screenshots.
- [ ] Separate true gameplay progression from mere screen-action success before balancing.
- [x] M1.5 offline multi-run reports with validated technical evidence, confidence intervals and explicit unmatched-cohort warnings.
- [x] M1.6 read-only offline replay of screenshot/trace evidence, validated against real WyrmForge run.
- [x] M1.7 human-label schema, original threat pixel evidence and offline scorer (instrumentation, not yet verified ground truth).
- [x] M1.8 independent offline clickable annotation UI with local JSON export, tests and real screenshot generation.
- [x] M1.8.1 local saved-label import, visual threat prediction overlays and real Chromium click/export/import verification.
- [x] M1.9 blind local annotation protocol, whole-run held-out split and reproducible temporal sampling.
- [ ] M1.9 real human-reviewed screenshots and independent held-out precision/recall with false positives and unknowns.

## M2 — Human Minds
- [ ] M2.0 game-neutral personality vectors with seeded shadow-only decision comparison, never hidden game state.
- [ ] Personality vectors (risk, skill, attention, patience, exploration).
- [ ] Short-term and persistent local memory.
- [ ] Full runs and decisions without hidden game state.

## M3 — Learning Lab
- [ ] Local offline training and first learned policy.
- [ ] Independent held-out trials and replay dataset.
- [ ] Compare a trained policy with a frozen heuristic baseline.

## M4 — Research Platform
- [ ] Build/upgrade balance with confidence intervals.
- [ ] UX hesitation and navigation-friction hypotheses.
- [ ] Local dashboard with screenshot evidence and regressions.
- [ ] Live Spectator Mode: watch one session, visual overlays and explainable decisions, local-first and free (design: [LIVE-SPECTATOR-DESIGN.md](LIVE-SPECTATOR-DESIGN.md)).
- [ ] Game-neutral adapters, dataset versioning and local scale-out.

## Gate for claiming an AI can play

Agent sees a screen, starts a run, moves, reacts to relevant visible feedback, and completes or loses an encounter **without predetermined navigation or secret engine state**.
