# ARGUS Roadmap

## M1 — Eyes & Hands
- [x] Independent .NET 10 repository scaffold and game-independent contracts.
- [x] External browser screenshot/input adapter in its own project.
- [x] Local PNG and JSONL trace format.
- [ ] CI build green on GitHub.
- [ ] Manually verify WyrmForge browser session.
- [ ] Detect visible screen regions and UI with confidence scores.
- [ ] Prototype genuine touch input for mobile.
- [ ] Agent navigates the menu using pixels only.
- [ ] Agent finishes or fails its first real encounter without scripted decision coordinates.

## M2 — Human Minds
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
- [ ] Game-neutral adapters, dataset versioning and local scale-out.

## Gate for claiming an AI can play

Agent sees a screen, starts a run, moves, reacts to relevant visible feedback, and completes or loses an encounter **without predetermined navigation or secret engine state**.
