# EUR 0-by-default operating policy

- Public GitHub repository and standard hosted CI runners.
- .NET 10, Playwright/Chromium, JSONL and optional SQLite: no license or API fees.
- Optional later: Python, PyTorch, Gymnasium and Stable-Baselines3 trained locally; CPU/GPU hardware and electricity still consume resources.
- No mandatory commercial LLM endpoints, paid GPU instances, hosted databases or external telemetry.

## CI guardrails

The checked-in workflow only restores/builds .NET and has an eight-minute timeout. Do not run continuous browser fleets or training on hosted CI. Repository owners should periodically inspect GitHub billing and quotas. Public standard-runner usage is free subject to GitHub's current terms; optional premium services are not.

## Storage and privacy

Results and screenshot traces remain local and Git-ignored. Bound run time, frame count, disk retention and amount of data before scaling. No automatic personal-data collection or recordings upload.

Reference: https://docs.github.com/en/billing/concepts/product-billing/github-actions
