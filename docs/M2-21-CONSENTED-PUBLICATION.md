# M2.21 — Publish original M2.20 development images with standing owner approval

**Owner clarification (18 October 2026):** permission to publish the 54 original
I/J/K screenshots and the three blank mobile annotation pages had ALREADY
been granted. Do not ask again and do not block on another consent question.

The original M2.20 immutable source plan still says
`publicPublicationAuthorized: false` because that was its state at capture;
**never rewrite that historical research manifest**. Record the owner's
subsequent confirmation in a separate *public publication manifest*.

## Frozen approved input

The **only** new approved raw-source archive is original M2.20 independent
review artifact `12133811970` from successful workflow
[39119368200](https://github.com/pjotrcasteel/Argus/actions/runs/39119368200),
ZIP SHA256
`a34a268537c500bef7f17c24c5721ad6308d6bd8c2f61634c7f04378a2ee4036`.
It has 54 original 390×844 PNG frames across I/J/K, 3 BLANK reviewer HTML
pages, the index and the frozen research plan (59 total files). Every
ZIP entry's SHA256 is pinned via whole-tree checksum
`aec60332d41855b83b5f3ad7b883dafa72b96a116f6926b9b61e12edf4d7fcbc`.
The source research plan contains run trace *digests* and coarse capture
phase diagnostics; it is **not** copied to GitHub Pages.

## Public checkpoint

The `m2-21-approved-publication.yml` workflow starts only from the verified
previous immutable public M2.19 snapshot. The local builder
`tools/m221/publish_consented_public.py` validates original source ZIP, all
54 frame steps/digests, expected run IDs, all-UNKNOWN HTML, no automatic
network submissions, and exact allowed paths. Only 54 PNG and 3 blank
HTML reviewers are added to the 122 prior static files (plus `.nojekyll`).

- Original 108 PNGs, 12 old HTML pages, and all existing localStorage
  namespaces are byte-identical to the published prior checkpoint.
- Home index gains three links.
- Public manifest gains a new, explicitly separately authorized M2.20
  original screenshot digest map and empty-human-truth notice.
- Total published site: 162 PNGs, 15 blank reviewers, one public manifest.
- **Never publish** owner-exported JSON, raw trace JSONL, research ZIP/plan,
  original held-out run-01, recorded ARGUS predictions, player coordinates,
  fitted weights or other game/source internals.

On successful production merge, the workflow creates a new persistent Git
branch `public-annotator-site-m220-checkpoint` (non-force). The actual
Pages deployment must be switched to that new checkpoint **in a separate
validated PR only after the new checkpoint exists**. No re-download of
expiring original artifact is needed afterwards.

## Mobile verification and next scientific step

Same real Chromium tests as M2.20 validate the new I/J/K blank reviewers
against actual source 390×844 PNGs at 320/375/430 iPhone widths, correct
original-pixel positions, round ring, explicit absent/unknown, private local
JSON export/import. Also run existing M2.18 checks to ensure no regression.

After deployment, the user can review:
- https://pjotrcasteel.github.io/Argus/annoteren/dev-i/
- https://pjotrcasteel.github.io/Argus/annoteren/dev-j/
- https://pjotrcasteel.github.io/Argus/annoteren/dev-k/

The user must choose actual visible player, not-visible or unknown; any
exported labels remain private. No model is fitted or deployed by this
publication and the original held-out run-01 remains untouched.
