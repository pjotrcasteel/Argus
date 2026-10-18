# M2.19 — Permanent previously public Pages site snapshot

The GitHub Pages build was still reading 5 historical GitHub Actions artifacts.
Actions artifacts expire. After expiry even the same approved public annotation
site might become impossible to reproduce. This change is **not new game
capture, review or model training**.

## Source / authorization

Use exactly the successful 2026-10-18 [M2.18 published Pages build artifact]
(https://github.com/pjotrcasteel/Argus/actions/runs/39116620943/artifacts/12132602391),
which is **already publicly available** under `https://pjotrcasteel.github.io/Argus/`.
Its ZIP SHA256 and a second whole-tree SHA256 are pinned in
`tools/m219/preserve_public_site.py`.

The tool fails closed for any change to any of the **122 immutable files**:
108 original 390×844 PNGs from the nine previously consented development
annotators; 12 blank public annotation HTML reviewers, the homepage and the
one public development manifest JSON.

The ZIP is **NOT** a full research archive. Only these already-published
pages/images can be preserved. Private human exports (including the 36 new
M2.18 player-center labels), hidden original M2.4 run-01, learning weights,
trace logs, and proprietary game source are absolutely excluded.

## Workflow

`.github/workflows/m2-19-preserve-public-site.yml` first downloads
the exact already-published Pages build with `actions: read`, independently
validates the full original archive and every entry, then deliberately injects
an unapproved fake label file to verify that the tool *rejects* it.

On a successful push to `main`, and **only** on main, the action commits
the 122 publicly approved assets plus the empty GitHub Pages `.nojekyll`
marker to a **new independent public repository branch**:

`public-annotator-site-checkpoint`

The branch push is deliberately **non-force**, so it refuses to overwrite
any existing snapshot. A new checkpoint after changing the public reviewer
requires a separately reviewed process and new pinned immutable hashes.
Nothing in this step publishes labels or claims a learned player model.

## Reproducibility

To retrieve this published snapshot after the original artifact expires:

```bash
git clone --branch public-annotator-site-checkpoint --single-branch \
  https://github.com/pjotrcasteel/Argus.git site
python3 tools/m219/preserve_public_site.py verify --site site
```

The second command comes from `main` (the validation script is not in the
orphan public asset branch). This verification must pass before deploying
old screenshots or changing anything from new engineering work.

## Following slice

**After the permanent branch is verified**, change Pages to build from
this checkpoint instead of retrieving expiring workflow artifacts, with
frozen per-file checks before every deployment. Do not swap the builder
until the branch actually exists, and do not claim long-term durability
from a successful artifact verification alone.

The first 36 authentic M2.18 human positions are available privately, but
the current per-run balanced-sample quality gate is NOT READY:
dev-f has 12 visible, 0 absent and only 1 off-center positive;
dev-g 11 visible/1 absent/7 off-center;
dev-h 11 visible/1 absent/5 off-center.
Those user exports and private reports stay outside GitHub and Pages.

## M2.19b — Pages switched to permanent Git checkpoint

After M2.19's [PR #51](https://github.com/pjotrcasteel/Argus/pull/51)
merged, the [main preservation action](https://github.com/pjotrcasteel/Argus/actions/runs/39117865061)
**SUCCEEDED** and created public branch
`public-annotator-site-checkpoint` at Git SHA
`039ba056584f929ac8c0cc69e33ba165383e6d4f`.
Only the 122 already-public static files and the empty `.nojekyll`
marker exist there, and every file was verified against the pinned digest.

The new normal `.github/workflows/mobile-annotator-pages.yml` build
fetches and archives the Git branch, then validates *every byte* against
`tools/m219/preserve_public_site.py verify` before running all existing
nine historical mobile review tests and the three M2.18 point-review
Chromium tests. A second admission check forbids private JSON (the only
allowed JSON is the already-public manifest), `run-01` held-out,
trace files, models or learned weights.

This **removes every older Actions artifact download from Pages**.
The public checkpoint must not be automatically overwritten or enlarged
to include later annotations; update only through a separately reviewed
new exact public-only snapshot with frozen SHA-256 digests and owner
approval for any additional screenshots. As before, there is no server
endpoint to accept human labels.

This is website transport/reproducibility only; the current real owner
M2.18 audit remains ST​​UDY_NOT_READY under per-run negative/position
balance. No live model was trained.
