# M2.38 — Three NEW post-update external browser captures and private blind reviews

## Goal and status boundary

M2.37 merged [PR #79](https://github.com/pjotrcasteel/Argus/pull/79)
with a research-only model/replay that MUST NOT be mistaken for a player
detector. M2.38 supplies **new independently captured original screenshots**
and immutable provenance for later human labeling. It does **not** train,
calibrate, test or activate live player identity, and it does not determine
WyrmForge wins, balance or success from a visual-smoke gate.

WyrmForge changed after older ARGUS studies: external town/UX/Arcane
Nexus PRs #120/#121. WyrmForge main SHA at the beginning of this milestone
was `84cf356c7f6363add86683fde75aaa7ab888c2ef`. GitHub Pages reported a
successful deployment subjob and browser smoke on that revision; a separate
external playtest collector connectivity check returned HTTP 403, so the
entire WyrmForge deploy workflow was red. **Neither GitHub source SHA nor
successful page deployment alone verifies the actual game code served to
an ARGUS browser**. Each capture records `gameVersion: null` and does not
assert matching world/seed/client version.

## Reproducible capture protocol

The dedicated `.github/workflows/m2-38-post-update-capture.yml` performs
strict synthetic/real-Chromium blind reviewer tests on pull requests; on
the tagged M2.38 **main merge** or manual `workflow_dispatch`, it runs
three separate Chromium browser processes with the existing ARGUS runner,
public external URL, 390x844 viewport, unchanged visual agent and profile,
a bounded 76 steps / 190 seconds per attempt, and max two tries per run.

The three run IDs are `m238-a`, `m238-b`, `m238-c`. Each run preserves
raw screenshots named `frame-NNNN.png`, the original `trace.jsonl`,
`summary.json`, and an immutable post-capture `run-manifest.json` containing
actual runner exit, UTC timestamps, ARGUS policy SHA, profile SHA, complete
trace SHA, unknown game version/outcome and the existing smoke gate.

`tools/m238/fresh_capture.py` rejects failed executions, missing frames,
non-PNG or non-RGB 390x844 originals, unexpected step ordering, incorrect
trace checksum, absent browser combat evidence and exact duplicated whole
trace bytes. **None may be silently repaired by declaring success**. A
WyrmForge start/menu change can produce a truthful capture `NOT_READY`
instead of 3 falsely successful game sessions.

From each admitted complete run, SHA-seeded chronological time bins
select 18 original whole screenshots without reading visual predictions,
labels, events, player locations or game internals. Exactly one authentic
immediate original predecessor is preserved per selected target, even when
that predecessor is outside the 18 frozen owner-review targets. Original
bytes are copied, not redrawn, OCR-labeled, recompressed or modified.

The offline per-run mobile reviewers use the previously browser-tested
tap geometry and export/import protocol, re-tested with synthetic AND
real browser PNGs at 320/375/430px widths. Result: 54 original targets,
54 preceding frames, original-byte SHA manifest, private review HTML,
zero supplied human labels. The output is intentionally NOT_EVALUATED;
reviewer exports are never generated as part of GitHub CI.

## Separate, non-public evidence and owner review

After a successful workflow:
- `argus-m238-real-external-source-no-owner-labels`: whole source runs
  with trace/process manifests, **7-day** GitHub Actions artifact only.
- `argus-m238-private-original-review-not-evaluated`: 54 frozen original
  blind targets, 54 authentic source predecessors, mobile HTML, hash
  manifest, **7-day** GitHub Actions artifact only.

Neither artifact is published to the ARGUS Git repository or Pages. Both
are development data, never the sealed O/P/Q prospective archive or old
M2.4 run-01. Human-owner labels must be produced separately after viewing
the unannotated original PNGs, never synthesized from ARGUS guessed positions.

Local commands on authorized owned copies:

```bash
python -m tools.m238.fresh_capture verify --runs /private/m238-real-source
python -m tools.m238.fresh_capture freeze --runs /private/m238-real-source --output /private/m238-blind-review
```

Do not confuse 54 sampled targets with a full uninterrupted sequential
history: full original trace/screenshots in the separate source artifact
are mandatory if executing the M2.37 temporal model. The M2.37 model
also still needs sufficiently reviewed distinct fitting and
whole-ABSENT calibration runs (>=4 genuine ABSENT controls from >=2
non-fitting runs) before it can replay these new held sessions.

## Next scientific gate

1. Check the actual post-update browser traces and screenshot evidence.
   If the external game flow changed, propose a separately tested
   screenshot-only visual navigation change in ARGUS; never change the
   game source or assume old menu labels remain correct.
2. If all three real sessions are complete, freeze and preserve the
   source artifacts before the short retention expires, then obtain
   owner-blind reviewed VISIBLE-center / ABSENT / UNKNOWN for each target.
3. Lock fitting/calibration, source SHA and source-run partitions BEFORE
   evaluating the new run labels. Reuse M2.37 exact 35px and per-run
   visible/absence gates; unknown does not mean absent; all three
   independent runs must pass for preliminary research-only GO.
4. Report NOT_READY/NO_GO on insufficient coverage or failure. Genuine
   self-improvement, independent learned play and live spectator remain
   future goals; do not claim implemented AI competence now.

No active ARGUS movement or WyrmForge source change occurs in M2.38.
