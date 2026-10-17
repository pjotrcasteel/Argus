# ARGUS M2.7 — Three independent development runs for harder visual evidence

## Goal

After genuine human review of M2.6b dev-a/dev-b, an offline development-only
Python reproduction of the current RGB shadow detector yielded dev-a 30 candidate
matches / 16 unmatched candidates, dev-b 43 matches / **only 2** unmatched. The
M2.6e readiness gate therefore honestly says **NOT_READY** for any learned model:
there are only two independent sessions, and dev-b has very few examples that
disagree with the human threat markings. M2.7 produces **more unbiased, visually
varied development evidence**, not a trained classifier or a promise to manufacture
ten negatives in every run.

## New, frozen development capture

Opt-in GitHub Actions [M2.7 three independent development sessions and blind mobile review](../.github/workflows/m2-7-development.yml)
runs **three fresh, independent Chromium processes** against the public WyrmForge
website, in series: `dev-c`, `dev-d`, and `dev-e`. Every run is capped at
48 observation steps and 150 seconds and uses the unchanged M1 screenshot-only
visual agent, the existing profile, viewport **390×844** and no game internals.

Before opening any candidate-scoring reports or human labels, the study tool
freezes exactly twelve original screenshots per run with the predeclared
`argus-m27-independent-development-frozen-20261017` seed: one selection from
each nonoverlapping temporal bin. This gives 36 **new** screenshots and a
`mode: development-only` plan containing only three development sessions.
The selection uses no detector predictions, enemies, outcomes, manual
annotations or cherry-picked frames. There are no M2.4 held-out images in
this pipeline.

The workflow rejects missing captures and duplicate trace SHA-256 digests,
stores screenshots/traces in separate run folders, records the UTC
provenance/policy fingerprint and checks the presence of the full fixed
sample and all-UNKNOWN human references. Separate browser processes and
different trace digests do **not** guarantee different procedural worlds,
enemy distributions, or statistical independence.

## Human review and non-enemy ambiguity

Two classes of error must remain explicit:

- A true visible enemy never proposed by the RGB segmentation is a missed
  human threat center, **not a negative training candidate**.
- A proposed candidate that lacks a matching human-marked center is an
  **unmatched visual candidate**, not a proven non-enemy. It may be scenery,
  magical visual effects, an occlusion, overlapping sprites or imperfect
  human review.

On each original PNG, the human reviewer should mark all confidently
visible enemy centers (including at outer edges or behind effects),
mark the player if visible and choose *unknown* when unsure. Do **not**
invent enemy points just to obtain more positives or negatives. A
reviewed `known` empty-threat set means no threat is visible in that
image, not that an RGB candidate is definitely decor.

Run quality, candidate accuracy and classifier readiness cannot be
calculated until human annotations are supplied. The real owner
reviews should stay local/private and correspond exactly to the
preregistered frame indices. Scoring must treat runs as the units of
replication, not 36 independent games.

## Mobile page artifact and permission

The workflow creates three **BLIND** iPhone-compatible review sites at
`annoteren/dev-c/`, `annoteren/dev-d/`, and `annoteren/dev-e/`.
Only the 36 selected original PNGs and the blank reference templates
enter the pages. No trace internals, actual agent decisions, predicted
player/enemy positions or existing private human labels are embedded.
The mobile Chromium smoke verifies touch placement, next/previous,
separate JSON download, and reload persistence **for all three**.

GitHub Actions retains two downloadable private workflow artifacts for
**seven days**, one with full unmodified research evidence and
`study-plan.json`, and one with a standalone mobile static site. **The
workflow does not publish any of these new screenshots on the public
GitHub Pages site.** The user approved publication of 24 specific
M2.6b screenshots previously; this is not automatic permission to
publish new M2.7 imagery. Ask before a separate Pages deployment.
The existing `dev-a`, `dev-b` and `run-02` annotation links must
remain intact.

**Owner authorization update, 2026-10-17:** the user explicitly approved
publishing *this additional* 36-image M2.7 cohort on public ARGUS GitHub Pages.
The separately validated `publish_m27.py` step extends the existing review
website with `annoteren/dev-c/`, `annoteren/dev-d/` and `annoteren/dev-e/`;
old `run-02/dev-a/dev-b` routes and their original screenshot and HTML bytes
must remain unchanged. The public page contains only the originally selected
WyrmForge screenshot pixels and blank human reference templates. It never
publishes source traces, candidate predictions, game internals or submitted
human labels. The build cross-verifies every one of the 36 new PNG byte
contents and sample indices against the frozen original M2.7 study plan,
runs mobile Chromium tap/export/reload tests for all 6 routes and verifies
the GitHub Pages deployment before declaring the HTTPS links usable.

Public links (claim working only after the main Pages deploy succeeds):
- `https://pjotrcasteel.github.io/Argus/annoteren/dev-c/`
- `https://pjotrcasteel.github.io/Argus/annoteren/dev-d/`
- `https://pjotrcasteel.github.io/Argus/annoteren/dev-e/`

As in earlier runs, export one separate `labels.json` per development run:
the JSON `runId` must remain `dev-c`, `dev-d` or `dev-e` respectively.
The review UI saves in the browser's local storage and does not transmit
human annotations to GitHub or ARGUS servers.

**Rebuild caveat:** the previous source review artifacts for M2.4/M2.6b and
the new M2.7 source artifacts are short-lived GitHub Actions files. The
site remains published after artifact expiration, but its next rebuild
requires preserved/recovered original data. Do not introduce a broken
rebuild that removes existing pages; archive vetted sources before expiry.

Once the owner sends three separately exported label files, run the
existing .NET 10 evaluation and M2.6d causal screenshot-feature audits
against this fixed cohort, alongside the previously **private** dev-a/b
audits. The readiness gate may still be blocked because dev-b remains
negative-scarce; additional data must not magically rewrite that cohort.

## Validation / next milestone

Require .NET 10 build/tests, new synthetic 3-run CLI + JSON/JS
verification, and successful **real** 3-session screenshot-only capture
with mobile touch/JSON-export checks before merging. Preserve the new
artifact evidence beyond its seven-day expiry in the owner's private
working environment. No active steering changes; no claim of human
recognition improvement or autonomous learning is warranted.

Next: arrange owner-approved iPhone review access, obtain all three
original-image human references, measure candidate false positives
and missed threats separately by run, and only then decide whether
there is enough realistic negative evidence for reproducible offline
learning. Keep the prior held-out run-01 untouched.
