# M2.27b — Restore only the failed authentic dev-S capture

The first genuine [M2.27 workflow run 39130303039](https://github.com/pjotrcasteel/Argus/actions/runs/39130303039)
exposed a real error. Two whole-browser source runs finished with honest
visual evidence:

- dev-R: 47 genuine original screenshots; runner exit 0,
  visual evidence gate true; source trace SHA256
  `4251af59aedc5fbd0477632b4ce1a83da0bc4c4f19fb32042257ca6e70b9fe07`.
- dev-T: 60 genuine original screenshots; runner exit 0,
  visual evidence gate true; source trace SHA256
  `a4ecd799b532da49dd83d6b3c3a77aa86c40172214c052d3f018e32ef5e710eb`.
- dev-S: runner **exit 1**, source summary missing and no valid
  independent visual evidence gate. Its preliminary trace SHA was
  `1bdd1a118a021e6af76ec35049065b918fd6a5c9c341605e7bb49029daa46bcc`.
  **Never call this an eligible original run**, synthesize frames, or
  retroactively set exit=0/gate=true.

The original full R/S/T source was uploaded in a private Actions artifact
`12138350509` with SHA256
`1458732e069f5b084fab989a6f9e59f07604a00b1709072817c059216fec8bd9`
(retention ends approximately 25 October 2026). It contains no owner
answers or sealed prospective O/P/Q files.

To avoid losing or altering the two successful independent processes,
M2.27b introduces `tools/m227/recovery_gate.py` and a one-time
honest recovery workflow:

1. Download precisely the hash-pinned original artifact.
2. Verify R and T bytes, trace SHA, 390×844 PNG header for every
   reported screenshot, chronological indices, exit=0, valid visual
   evidence, and genuinely distinct source traces. Never overwrite
   those two runs.
3. Delete only failed old S source, re-run the **same unmodified
   ARGUS visual agent**, same external game, viewport, ordinary
   controls and existing profile for S. Allow **up to three bounded
   attempts**, but accept one only if its own real process exits 0,
   has >=36 authentic original frames and passes the original
   visual evidence and screenshot chain.
4. Run `recovery_gate --include-s` on all three independent sources,
   then freeze 18 independent SHA-chronological original target frames
   per run plus 18 true original immediate preceding frames. No
   synthetic/interpolated contexts, no inferred positions or outcomes.
5. Test the real original-image mobile blind R/S/T reviewers in
   Chromium and upload distinct private review/context and original
   whole-source artifacts with 7-day retention.

The recovery process may cause the **source capture timestamps and
Git SHA provenance to differ** between original R/T and newly
captured S. This must be retained honestly. Source selection uses
identical frozen M2.27 seed/algorithm and the game-facing agent code
remains unchanged. We do NOT claim the resulting run set is an
independent prospective validation; it is new development only.

If no source-S attempt succeeds, report the failed capture and do
not release M2.28 public R/S/T reviewer pages. The separate original
M2.23 sealed O/P/Q prospective cohort and original M2.4 run-01
remain unused. No live ARGUS steering model is changed.
