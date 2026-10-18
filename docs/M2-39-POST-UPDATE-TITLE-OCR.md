# M2.39 — Recover visual start navigation after WyrmForge town/title update

## Observed authentic failure

On [M2.38 main research capture 39157000315](https://github.com/pjotrcasteel/Argus/actions/runs/39157000315),
the `m238-a` browser was attempted twice with the actual post-update
external WyrmForge website. Each attempt saved 77 original viewport
screenshots and failed the visual-combat gate with:

- 0 menu clicks, 0 route clicks, 0 inferred player frames;
- all 77 phases `unrecognized` with repeated safe `Wait` actions;
- 0 actual actions into the game beyond waiting.

The genuine screenshot was preserved privately as Actions artifact
`12147208748` (7 days). Original frame 0001 shows a prominently filled
gold button with the **visible** text `START GAME` at the bottom of the
390×844 screen. No game source, DOM, internal UI/game state or hidden
telemetry was inspected.

The recorded OCR recognized `WYRMFORGE` and audio UI text but did **not**
see `START GAME`, preventing menu matching entirely. Independently replaying
the exact original image against local Tesseract:

| Tesseract mode | Visible output |
| --- | --- |
| `--psm 11` (old sparse scan) | WYRMFORGE, audio labels; **no START GAME** |
| `--psm 12` (sparse + orientation) | **START GAME**, confidence ~96%, original coordinates (115,759) etc |
| `--psm 3` (auto) | **START GAME**, similar original coordinates |

M2.39 chooses a minimal game-neutral OCR segmentation improvement
(`--psm 12`) and adds the **visible** `Start Game` action to the external
WyrmForge profile. No fixed button coordinates, game-name detection, palette
assumption, injection, data integration or learned model changes.

The new deterministic MSTests verify original-size coordinate preservation
and `START GAME` line matching, using the actual measured public-text
TSV locations without storing any screenshot or private owner annotation.
The next normal real-browser M1 smoke must verify at least one menu click
and real gameplay evidence. A succeeding unit test is **not** proof that
the changed menu is navigable, and M1 smoke success is not a player
identity or game-balancing metric.

## Important limitations

- M2.38 failure remains a true fail; do not rewrite or delete original
  source/action traces, or invent 54 blind target reviews from the failed run.
- The new title is only the first visible step. WyrmForge has redesigned
  the town and may expose *additional* screens unknown to ARGUS.
  Read the next authentic traces and adapt the visual profile/recognition
  incrementally based solely on screenshot evidence.
- No change to M2.35–M2.37 learned-shape weights, private calibration,
  old source labels or the live combat steering.
- Original M2.23 O/P/Q and M2.4 run-01 remain sealed and untouched.

## Next

Run PR .NET tests **and real external browser smoke**. If genuinely
reaching gameplay, merge then trigger M2.38 three-run independent source
capture again, with explicit attempt provenance. The resulting three
whole runs must still pass exact PNG and manifest integrity before
sampling independent owner-blind reviews. If any run fails, report
`NOT_READY`, preserve its authentic evidence and diagnose the next
visible navigation barrier.
