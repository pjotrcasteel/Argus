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

## Second original-game validation (PR #81, first attempt)

The authentic WyrmForge PR browser on the corrected, parseable profile
recorded **one genuine title menu click** at step 1, validating the
`START GAME` OCR fix. It then reached the redesigned **REFUGE** town
screen but remained there with **zero route clicks** and no terminal,
ending the 57-frame bounded smoke gate as **FAIL**; 52 purported
"player-located" frames actually pointed at the decorative town avatar
and are **not genuine combat or validated player identity**.
Evidence: [Actions run 39158225513](https://github.com/pjotrcasteel/Argus/actions/runs/39158225513),
short-lived original screenshot artifact `12148345532`.

On the original town PNG, there is a dark rectangular
`BEGIN FIRST HUNT` button. Actual `--psm 12` OCR on the original
390x844 image splits its label into high-confidence `BE` and
`FIRST HUNT`, at original y=704; neither line matches the previous
full `Begin First Hunt` profile action. Offline doubling the exact
original RGB PNG improves the full label recognition, but merely
adding a source-proven `First Hunt` alternate visible label is the
minimal change (still click where OCR sees text, no coordinates).

More importantly, the baseline agent began moving across the town
after three post-title empty scans, using **menu-click history alone**
as permission to explore. M2.39 now refuses to start any exploratory
keyboard movement without an actual visible gameplay cue, even after
a valid earlier menu click. A menu/state transition is not gameplay
evidence. Synthetic tests replay the two consecutive states.

These tests do NOT yet prove the new first-hunt click enters combat.
Re-run PR real browser smoke and inspect fresh evidence, including
any additional newly introduced town, preparation, or magic-selection
screen. Do not report the decorative town sprite as 52 successful
combat player detections.

## WyrmForge changed again during live validation

On 2026-10-18 WyrmForge merged [PR #122](https://github.com/pjotrcasteel/Wyrmforge/pull/122),
main SHA `54a522eaedaf403ff841891819da9e5454113425`,
redesigning the welcome/courtyard and consolidating audio settings.
The new actual PR screenshot changed to a `START GAME >`
button around source y=697, rather than the previous `START GAME`
at y=759. The first updated `--psm 12` PR live browser on this *new*
version once again waited all 57 frames with **0 menu clicks**;
[run 39158699139](https://github.com/pjotrcasteel/Argus/actions/runs/39158699139),
real original image evidence artifact `12147678823`.

Repeating exact local OCR on both original screenshots shows:
- Previous 0.0.118 title before #122: `--psm 3` recognizes
  `START GAME`; `--psm 11` misses it.
- New #122 title: `--psm 3` recognizes `START GAME >`
  (START original box x117/y697 confidence 96%, GAME x183/y697
  confidence 76%); `--psm 12` misses it.
- The earlier #121 Refuge `BEGIN FIRST HUNT` appears in original
  390x844 `--psm 3` as high-confidence `FIRST HUNT`.

Consequently M2.39 switches to **ordinary automatic full-page
Tesseract `--psm 3`**, no fixed click location, and keeps the small
source-observed `Start Game` and `First Hunt` external profile labels.
New deterministic tests cover the actual original OCR text boxes for
both title revisions, the first-hunt partial word grouping, and
failure-to-start-combat protection in town. **Neither old nor new
smoke failures may be hidden by selecting a convenient site version.**
A fresh genuine browser run is required after this change.

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

## Third external-browser experiment and bounded OCR remediation (2026-10-19)

The post-#122 PR [browser run 39160016344](https://github.com/pjotrcasteel/Argus/actions/runs/39160016344)
**actually navigated** the START GAME control (step 1), BEGIN FIRST HUNT (3),
a primary spell (5), a screenshot-located route marker (7), and an
enabled route confirmation (9), then entered apparent combat: steps
17–24 have eight image-derived player candidates and ordinary movement.
This is evidence of navigation into a combat-like screen, **not**
verified player identity or success in a hunt. The run still failed:
after a choice overlay at step 25, repeated OCR operations consumed
approximately ten seconds each and the bounded 180-second run stopped
at step 40 before writing a valid summary. [Its unmodified image/trace
artifact](https://github.com/pjotrcasteel/Argus/actions/runs/39160016344/artifacts/12147544522)
is the source of the timing diagnosis. DO NOT count this timed-out run as PASS.

The PR now also:

- enforces separate **3-second full-viewport** and **2-second footer**
  Tesseract budgets instead of one 10-second compound timeout; retains
  full-page visible text if only the footer scan is too slow;
- performs OCR on every navigation/route frame but samples every third
  **already-established exploration** screenshot; all frames still use
  actual visual pixel observation and motion/steering, with skipped OCR
  explicitly marked as text scan deferred in trace reasoning;
- tests that a new terminal label is still recognized on the next
  sampled frame (at most two intervening exploration frames).

The deliberate cost is up to two extra exploration actions before a
new choice/result overlay is recognized. It is an **M1 smoke transport
trade-off**, not a learned perception improvement. These changes must
be checked by new real-browser Actions and deterministic .NET CI; do
not change accuracy gates or retroactively re-score earlier failures.

## Next

Run PR .NET tests **and real external browser smoke**. If genuinely
reaching gameplay, merge then trigger M2.38 three-run independent source
capture again, with explicit attempt provenance. The resulting three
whole runs must still pass exact PNG and manifest integrity before
sampling independent owner-blind reviews. If any run fails, report
`NOT_READY`, preserve its authentic evidence and diagnose the next
visible navigation barrier.
