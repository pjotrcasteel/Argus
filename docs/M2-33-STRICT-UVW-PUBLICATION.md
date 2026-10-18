# M2.33 — new consent-approved UVW original target reviewers; no private temporal inputs

**Pre-publication source verification: 2026-10-18.** The owner's existing
standing permission authorizes only genuine original **development target**
screenshots and entirely blank mobile review HTML. It does not authorize
publishing the immediate-prior context images, user labels/coordinates,
actual whole-game traces, model parameters, O/P/Q prospective images, or
M2.4 run-01.

## Frozen and independently checked sources

- [M2.32 real development capture](https://github.com/pjotrcasteel/Argus/actions/runs/39142578886)
  **SUCCESS**. Three separate actual external black-box browser sessions
  `dev-u=45`, `dev-v=77`, `dev-w=47` original frames. Identified exactly
  **54 original targets** (18 SHA-seeded chronological steps per run,
  53 unique image SHA digests) and **54 authentic immediate predecessor**
  originals. Three all-UNKNOWN mobile browser-review pages.
- Frozen authenticated 113-member original review ZIP:
  `12142830180`, SHA256
  `ab1c338aea8af582b0c93de2f7c7b4f11528b62c50cf03ce1fd3e013dff7e26f`.
  Independently checked by CRC, all 108 original PNG SHA, original step/previous
  source chronology, 390×844 geometry and all-UNKNOWN provenance.
  Its original GitHub Actions artifact expires 2026-10-25; preserve private
  copy outside source control.
- Old immutable public target-only website branch
  `public-annotator-site-m227-development` contains 294 files: 270
  original PNGs and 21 blank reviewer pages, plus index, digest manifest
  and .nojekyll. Old complete tree hash pinned by M2.28:
  `ace883e8b2f120c6d22669b32dd47fa18a4dc20bdf359cae8f2857d9de7a8759`.

## Strict new public-only boundary

`tools/m233/publish_approved_uvw.py` consumes the exact frozen source ZIP,
checks the prior approved public tree, all newly selected original target
pixels, all forbidden context members and reviewer blankness **before**
constructing a possible website. It refuses unexpected ZIP members and
duplicate original steps. It copies **only** 54 target PNG and three
blank HTML reviewers from the genuine new source; no 54 temporal prior
images or other private/research artifacts are admitted.

Final exact static site contains **351 files**:
- **324** original authorized development screenshots, 390×844;
- **24** entirely blank on-device-only reviewer HTML pages;
- one sanitized public original image SHA digest manifest, home page
  and unchanged .nojekyll.

All **294** prior public files remain byte-identical apart from intentionally
updated homepage links and public manifest, after strict old-tree verification.
Original new reviewers use separate local-storage and JSON filename keys
`argus-m232-player-point-` and `m232-dev-{u,v,w}-player-points.json`.
There is NO automatic label upload, no game recognition truth in the HTML,
no source-game API/DOM or external paid service.

**Independently frozen whole website tree SHA256:**

`e8bbe08b8f4cc1310b3c1322bb0a0517743772899a14bfe1880e85d9611cffee`

This is the SHA computed from real original source (not generated samples)
during independently green
[PR Actions 39143304761](https://github.com/pjotrcasteel/Argus/actions/runs/39143304761)
after earlier 294-file checkpoint SHA verification and actual mobile Chromium
tap/download/import/reload tests for **all three new U/V/W routes and
historical published study routes**. The workflow emitted ONLY a digest text
artifact, never original screenshots as PR artifacts. It was pinned in
source **before merging**, and all future builds must revalidate it.

## Deployment sequencing

1. On the source-pinned PR, require synthetic allowlist tests,
   independently SHA-pinned 351-file build, real mobile Chromium regression
   and standard .NET CI all SUCCESS. Do not merge with unknown tree checksum.
2. A green M2.33 merge triggers a new non-force, immutable Git branch
   `public-annotator-site-m232-development` with **only this validated public
   site**. Preserve previous public Git snapshots untouched.
3. A **separate reviewed PR** changes the GitHub Pages workflow to consume
   only that new pinned branch, verifies all old/new JS and mobile tests
   and publishes after the actual production Pages deploy job is SUCCESS.
   The intended URLs *after* verified production deployment:
   `/annoteren/dev-u/`, `/annoteren/dev-v/`, `/annoteren/dev-w/`.
4. The new mobile forms begin UNKNOWN, **not pre-labeled**. The owner's
   future private JSON exports need exact original SHA / run / step source
   admission. No new learned model was promoted or genuinely player-validated
   merely because browser tests or public annotation work succeeded.

Keep the original prospective O/P/Q bytes SHA-sealed/unopened; do not
publish or access them, and keep active ARGUS gameplay unchanged.
