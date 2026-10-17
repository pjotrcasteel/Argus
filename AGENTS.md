# ARGUS — instructions for continuing agents

This repository is developed collaboratively, across conversations. **START HERE**:

1. Read [docs/CONTINUITY.md](docs/CONTINUITY.md) in full: last verified state, engineering judgement, next priority, limitations, and links.
2. Inspect **current** `main`, open pull requests/issues and CI before making edits. Do not trust a stale handover over GitHub.
3. Work only in ARGUS; WyrmForge is an **external, unmodified visual target**. No source/DOM/API/game-engine coupling or paid mandatory services.
4. Use .NET 10 and deterministic MSTest tests; prefer independently verified medium-size vertical slices and proper CI/real-browser evidence.
5. Record uncertainties. A visually detected sprite is a *candidate*, not automatically an enemy; a smoke-pass is not a victory/balancing result.
6. Update **docs/CONTINUITY.md** at the end of each substantive slice, including date, PR, CI/evidence and immediate next step. Keep the README pointing to it.

The user's shorthand in any future conversation is simply: **"continue with ARGUS https://github.com/pjotrcasteel/Argus"**.
