# ARGUS — continuity and engineering handover

> **Read this first when resuming the project.** Repository: https://github.com/pjotrcasteel/Argus
>
> Last inspected and updated: **2026-10-17**. This document describes *publicly shareable engineering state, decisions, hypotheses and next actions*, not private reasoning. It is **not** a substitute for checking the current GitHub `main`, open PRs, CI and fresh evidence.

## One-sentence restart instruction

"Continue with ARGUS: https://github.com/pjotrcasteel/Argus . Read `AGENTS.md` and `docs/CONTINUITY.md` first; check current code, open PRs and actions, then implement the next validated vertical slice. Update the handover before stopping."

## Mission / owner's intent

Create a **game-independent, standalone, free-by-default autonomous visual playtesting laboratory**. Its first game is WyrmForge, being developed separately by another agent. ARGUS must act from screenshots and ordinary player input like a human player and independently evaluate navigation, UX and eventual game balance. Future targets may include permitted desktop games, but only if their terms/permission allow automated play.

- **Never integrate ARGUS into WyrmForge's codebase.** No shared assemblies, DOM introspection, game JS injection, internal endpoints, memory inspection, debug state or privileged telemetry.
- **No mandatory paid APIs, SaaS, cloud GPUs, databases or streaming services.** `.NET 10`, Playwright/Chromium, local Tesseract OCR, MSTest and local PNG/JSONL are the foundation.
- Personalities (risk-taking, patience, skill, attention, exploration etc.) and **genuine self-learning** are future goals; do not imply either is implemented today.
- Work in **small-to-medium tested vertical slices**, use PRs, check GitHub Actions, and inspect *real screenshot/trace evidence* before merging. Prefer correctness to feature count; do not claim a green CI job proves an AI plays competently.
- The owner explicitly wants a **future Live Spectator Mode** where one real run can be watched with visual overlays and its decision explanations. Design: [LIVE-SPECTATOR-DESIGN.md](LIVE-SPECTATOR-DESIGN.md); tracking: [issue #11](https://github.com/pjotrcasteel/Argus/issues/11). **Not implemented yet.**

## Sources of truth

1. Current `main` code, projects and configuration: [repository](https://github.com/pjotrcasteel/Argus).
2. [Open PRs](https://github.com/pjotrcasteel/Argus/pulls) and [GitHub Actions](https://github.com/pjotrcasteel/Argus/actions); never assume an old PR is still open or a historical build describes latest `main`.
3. [Roadmap](ROADMAP.md), [architecture](ARCHITECTURE.md), [game adapters](GAME_ADAPTERS.md), [cost guardrails](COSTS.md), [M1.4 motion evidence](M1-4-SCENE-MOTION.md), [Live Spectator design](LIVE-SPECTATOR-DESIGN.md).
4. CI screenshot/trace artifacts from live WyrmForge runs **expire quickly** (two-day retention). If unavailable, re-run the existing bounded live workflow or run locally; do not invent a past screenshot.

## Current implementation (as of main after PR #24)

- `Argus.Core`: screenshots, actions, agent/profile interfaces, game-neutral evidence and session summary.
- `Argus.Adapters.Browser`: black-box Playwright screenshots and ordinary keyboard/mouse; local Tesseract OCR with bounded per-frame timeout.
- `Argus.Agents.Visual`: OCR-driven menu/magic-card clicks; visually identified circular route nodes; optional button fill detection; screen-pixel mage palette tracking with temporal prior; uncertain candidate-threat segmentation and simple evasive policy; separate **diagnostic-only** camera/background translation and scene-relative candidate-motion estimator.
- `Argus.Runner`: bounded single-session CLI; PNG screenshots, JSONL actions + reasons, `summary.json` evidence report; optional `--require-combat-vision` gate.
- `Argus.Analysis` + `Argus.Reporter`: **offline-only** cohort reader and reporting CLI. Read multiple independent `summary.json` runs, validate integrity, optionally compare controlled-manifest metadata, and produce local `cohort.json` / `cohort.md`. Wilson 95% interval is for visual smoke-gate frequency only. See [M1.5 cohort reports](M1-5-COHORT-REPORTS.md).
- `Argus.Replay` + `Argus.Replay.Tool`: validate saved `trace.jsonl` and numbered PNG screenshots, and generate local `replay.html` with play/pause, timeline seek, frame steps, inferred player position, actions and decision reasons. No server/integration/streaming; see [M1.6 replay](M1-6-LOCAL-REPLAY.md).
- `Argus.Evaluation` + `Argus.Evaluation.Tool`: offline, human-labeled **visible pixel reference evaluation**. Creates all-`unknown` templates, validates player/threat annotations, evaluates the *recorded* locations with TP/FP/FN/TN and nullable precision/recall/F1; old traces with missing threat coordinates remain `unknown`. [M1.7 docs](M1-7-PERCEPTION-EVALUATION.md).
- `Argus.Evaluation.AnnotationPageBuilder`: independent, offline `annotate.html` with clickable player/threat markers, absent/unknown controls, ARGUS predicted-position overlay and local `labels.json` export. Unlike the replay, the **annotation file** is editable, but neither page controls the game. [M1.8 guide](M1-8-ANNOTATION-STUDIO.md).
- `Argus.Evaluation.StudyPlanner` + `Argus.Study.Tool`: SHA-256-seeded full-run development/held-out splits, temporal-bin sampling and local `study-plan.json` plus `blind-annotate.html` review pages. Blinded pages **remove** prediction coordinates, actions and reasons before serialization. See [M1.9 blind study protocol](M1-9-BLIND-STUDY.md).
- `Argus.Personas` + `Argus.Persona.Tool`: game-neutral typed playstyle traits (risk, exploration, patience, attention, motor precision), conservative/adventurer/daredevil presets, and purely **offline shadow-only** proposals on the **same** already-recorded screen observations. Outputs `persona-shadow.jsonl` without executing any game input; [M2.0 docs](M2-0-PERSONA-SHADOW.md).
- `Argus.Personas.PersonaContrastAnalyzer`: explicit per-frame/per-persona action counts and pairwise wait-vs-move, direction, timing and full-action divergence; shows missing detection evidence separately. Existing offline CLI writes `persona-contrast.json` and `persona-contrast.md` without alternative game input. See [M2.1 metrics](M2-1-PERSONA-CONTRAST.md).
- `Argus.Persona.Cohort.Tool` + `PersonaCohortStudy`: multiple recorded external-game sessions (2–128), exact duplicate trace rejection, optionally matched game/profile/viewport/policy metadata and **per-run** unweighted distributions of action/direction differences; results `persona-cohort.json` and `persona-cohort.md`. `PersonaTraitAblations` compares the five traits from 0→1 with a fixed random stream, so profile IDs don't masquerade as effects. The old single-run persona CLI now adds `persona-ablation.json`; [M2.2 docs](M2-2-COHORT-ABLATIONS.md).
- `.github/workflows/m2-research-cohort.yml`: **opt-in** bounded two-session public WyrmForge Actions workflow; independently launches two real browser processes, writes UTC provenance, actual ARGUS SHA, exact profile hash and viewport, leaves game version/outcomes unknown, and runs M2.2 on the two real traces. Does not run on ordinary PRs or a schedule; [M2.3 protocol](M2-3-REAL-RESEARCH.md).
- `Argus.Evaluation.StudyQualityEvaluation` + `Argus.Study.Quality.Tool`: validate preregistered deterministic per-run blind frame/partition assignments, evaluate only optionally imported **human-reviewed** `labels.json` files against recorded original visual predictions, and report per-run player/threat precision, recall, unknown coverage and development-versus-held-out run-level medians. Absent/all-unknown labels yield `NOT_EVALUATED` without fabricated metrics. [M2.4 protocol](M2-4-BLIND-QUALITY.md).
- `Argus.Tests`: deterministic MSTest tests for visual UI, geometry, tracking, motion, cohorts, replay, perception scoring, persona contrast, controlled trait ablation and HTML/JS safety.
- `.github/workflows/ci.yml`: .NET 10 build/tests. `m1-live-smoke.yml`: *bounded* browser WyrmForge run; collects short-lived evidence; requires evidence gate.
- Already observed by the **external** player: open the public WyrmForge page, begin a hunt, select Arcane Orb, choose a route and enter the trail, move, choose upgrades and sometimes recognize an end screen. These are heuristic actions, NOT AI training.

Recent merged work: [PR #10 motion evidence](https://github.com/pjotrcasteel/Argus/pull/10), [PR #12 offline cohort reports](https://github.com/pjotrcasteel/Argus/pull/12), [PR #13 local replay](https://github.com/pjotrcasteel/Argus/pull/13), [PR #14 perception label scoring](https://github.com/pjotrcasteel/Argus/pull/14), [PR #15 local annotation studio](https://github.com/pjotrcasteel/Argus/pull/15), [PR #18 verified label re-import](https://github.com/pjotrcasteel/Argus/pull/18), [PR #19 blinded run studies](https://github.com/pjotrcasteel/Argus/pull/19), [PR #20 persona shadow hypotheses](https://github.com/pjotrcasteel/Argus/pull/20), [PR #21 measured persona contrasts](https://github.com/pjotrcasteel/Argus/pull/21), [PR #22 multi-run cohorts and trait controls](https://github.com/pjotrcasteel/Argus/pull/22), [PR #23 real session capture](https://github.com/pjotrcasteel/Argus/pull/23), [PR #24 blinded reference quality](https://github.com/pjotrcasteel/Argus/pull/24).

## Prior motion evidence experiment (2026-10-17)

[GitHub Actions run 39022431491](https://github.com/pjotrcasteel/Argus/actions/runs/39022431491), on PR #10. Build/unit tests and live workflow passed. Its `summary.json` reported:

| Item | Value |
| --- | ---: |
| Frames observed | 57 |
| Menu clicks | 2 |
| Route clicks | 2 |
| Upgrade clicks | 2 |
| Frames with inferred player | 28 |
| Candidate avoidance frames | 17 |
| Frames missing player while exploring | 13 |
| Estimated background-shift frames | 8 |
| Frames with possible moving candidates | 5 |
| Frames with possible stationary candidates | 1 |
| Terminal stops | 0 |
| Visual-combat evidence gate | PASS |

**Interpretation:** The game was reached and some visible behavior was recorded. Only 8/57 frames got camera-motion estimates. These 5 moving-candidate frames do **not** establish five actual enemies or successful dodges. The existing gate is intentionally a *transport/perception* smoke test, not a gameplay quality, accuracy or balance benchmark. The 0 terminal stops in this run is not a failure of the bounded smoke, but no completed run may be asserted for that example.

## Latest verified GitHub CI and live experiment (2026-10-17)

- [PR #12](https://github.com/pjotrcasteel/Argus/pull/12) was merged into `main` as `db166f08e997a9528d3f785b36054f6a88f7d1c0`.
- [.NET build + 42 deterministic tests](https://github.com/pjotrcasteel/Argus/actions/runs/39023654750) succeeded after fixing an incorrect test fixture and enforcing **missing camera counters ≠ zero**.
- [Live WyrmForge smoke](https://github.com/pjotrcasteel/Argus/actions/runs/39023654745) passed existing gate, independent of the offline reporter: **57 frames, 2 route interactions, 2 upgrades, player seen in 37 frames, 32 candidate-avoidance frames, 5 missing-player exploration frames, 0 terminal stops, 0 camera-motion estimates**.
- **Interpretation:** the experiment reached combat; `0/37` estimated camera shifts in this run is a major reliability issue, not a finding of zero true movement. Do not use current candidate velocity as objective balancing data.
- No actual multi-run real-game cohort report has yet been generated from independent sessions. Reporter correctness is verified using deterministic test fixtures, not a live balancing experiment.

## Latest verified replay milestone (2026-10-17)

- [PR #13](https://github.com/pjotrcasteel/Argus/pull/13) merged on `main` at `c6a42867fd6c42049151a1ce6e66489e508aea58`.
- [.NET CI run 39024084790](https://github.com/pjotrcasteel/Argus/actions/runs/39024084790): successful build and **48/48 tests**.
- [Real WyrmForge run 39024084743](https://github.com/pjotrcasteel/Argus/actions/runs/39024084743): **52 frames**, 34 frames with inferred player, 2 route interactions, 1 visible terminal stop; existing smoke gate passed.
- Crucially, that live workflow **generated `replay.html` from the actual saved PNG + JSONL trace** via `Argus.Replay.Tool`. Its [short-lived artifact](https://github.com/pjotrcasteel/Argus/actions/runs/39024084743/artifacts/12098166006) includes evidence and viewer. Artifact retention is two days, so recreate if unavailable.
- **Scope:** offline HTML replay, not live streaming. The page was generated successfully by CI against real data; interactive display has not been human-reviewed in a local browser and must not be claimed to be UX-validated.

## Latest verified perception-instrumentation milestone (2026-10-17)

- [PR #14](https://github.com/pjotrcasteel/Argus/pull/14) merged to `main`, commit `b27249b372109a125c2b26c54b4a44133ededcd5`.
- [.NET CI run 39025485807](https://github.com/pjotrcasteel/Argus/actions/runs/39025485807): **54/54 deterministic tests passed**, build succeeded.
- [Public WyrmForge run 39025485863](https://github.com/pjotrcasteel/Argus/actions/runs/39025485863) passed browser smoke, 18 frames with an inferred player, 2 route clicks, 1 visible terminal stop, and wrote an all-`unknown` **16-frame annotation template** from the real trace. [Short-lived artifact](https://github.com/pjotrcasteel/Argus/actions/runs/39025485863/artifacts/12098656427).
- **No manually verified ground-truth frames were created yet**. The annotation scorer is tested on synthetic fixtures only. **Do not claim player/enemy precision, recall or quality thresholds yet.** The new trace now records threat candidate screen centers in addition to counts. Some combat frames without a located player still have unavailable threat evidence, not zero detections.

## Latest verified perception instrumentation (2026-10-17)

- [PR #14](https://github.com/pjotrcasteel/Argus/pull/14) merged into `main` at `b27249b372109a125c2b26c54b4a44133ededcd5`.
- [.NET CI run 39025485807](https://github.com/pjotrcasteel/Argus/actions/runs/39025485807): successful build and **54/54 tests**.
- [External WyrmForge smoke run 39025485863](https://github.com/pjotrcasteel/Argus/actions/runs/39025485863): existing visual-combat gate passed; 18 inferred player frames, 2 route clicks, 1 terminal stop. Generated [replay and 16 all-unknown frames for manual annotation](https://github.com/pjotrcasteel/Argus/actions/runs/39025485863/artifacts/12098656427).
- IMPORTANT: no labels were independently verified; no real precision/recall figure exists yet. New tool proves schema, input validation and deterministic scoring, not detector quality.

## Latest verified annotation-UI milestone (2026-10-17)

- [PR #15](https://github.com/pjotrcasteel/Argus/pull/15) merged on `main` at `6bb3660527eca041759bacb6255cfdbd761e913b`.
- [.NET CI run 39025964482](https://github.com/pjotrcasteel/Argus/actions/runs/39025964482): successful build and **58/58 tests**.
- [Live WyrmForge run 39025964485](https://github.com/pjotrcasteel/Argus/actions/runs/39025964485): 57 frames, 35 frames with inferred player, 2 route clicks, 2 upgrade clicks, 7 inferred camera shifts, 0 terminal stops; visual-combat gate PASS. [Short-lived artifact](https://github.com/pjotrcasteel/Argus/actions/runs/39025964485/artifacts/12098717008) contains real screenshots, replay, **16-frame all-unknown template**, and locally generated `annotate.html`.
- Generated JavaScript was checked with `node --check` in CI; no syntax failures. **Real interactive usability on an actual user's browser and manual annotation accuracy have not yet been independently validated.**
- The template and UI **do not make automatic labels**; no true precision/recall exists yet for WyrmForge player/enemy recognition.

## Latest M1.9 blind-study milestone (2026-10-17)

- [PR #19](https://github.com/pjotrcasteel/Argus/pull/19) merged to `main` at `460f14a9606c0d675ae77d6c79039959679adf84`.
- [.NET CI 39028273654](https://github.com/pjotrcasteel/Argus/actions/runs/39028273654): successful build, **68/68 MSTest tests**, and an end-to-end `Argus.Study.Tool` dry-run on **synthetic** trace fixtures; proves reproducible whole-session split, not scientific independence of artificial data.
- [Real WyrmForge smoke 39028273655](https://github.com/pjotrcasteel/Argus/actions/runs/39028273655): visual-combat evidence gate PASS (21 inferred player frames, 2 route clicks, 1 terminal stop). Live artifact [12099501901](https://github.com/pjotrcasteel/Argus/actions/runs/39028273655/artifacts/12099501901) contains the locally generated `blind-annotate.html`; Node validates generated page syntax and the existing Chromium click/export/import smoke passed.
- Independent per-run study groups require **at least two real recorded runs**. The tool rejects exact duplicate traces, but names/checksums alone cannot prove the sessions were independently executed. All labels start unknown.
- **No real held-out human annotations, player/enemy precision/recall or balance comparisons exist yet.** The tool has no AI learning and does not affect gameplay policy.

## Latest M2.0 persona shadow milestone (2026-10-17)

- [PR #20](https://github.com/pjotrcasteel/Argus/pull/20) merged to `main` at `4efa28b91e8001000f94af4f8b9582d1c8a43e32`.
- [.NET CI 39028643055](https://github.com/pjotrcasteel/Argus/actions/runs/39028643055): build succeeded and **75/75 deterministic tests passed**; synthetic tests prove conservative vs daredevil propose different directions for the **same** visible candidate.
- [Live WyrmForge run 39028643042](https://github.com/pjotrcasteel/Argus/actions/runs/39028643042): original external visual-combat gate PASS (49 frames; 31 inferred-player frames, 2 route clicks, 1 terminal stop). The separate persona tool read the already-saved frames and produced **33 hypothetical actions per preset** (`conservative`, `adventurer`, `daredevil`). [Short-lived artifact](https://github.com/pjotrcasteel/Argus/actions/runs/39028643042/artifacts/12099592057) includes `persona-shadow.jsonl` along with screenshots, replay and blind annotations.
- None of the shadow proposals was sent to the game. Equal per-profile action counts do NOT establish different behavior frequencies, human realism or better/worse outcomes; this needs explicit per-frame divergence analysis and later actual verified studies.
- These are **designed policy heuristics, NOT trained models or real individual player classifications**. Active baseline agent, game interaction and lack of source integration are unchanged.

## Latest M2.1 quantified persona divergence (2026-10-17)

- [PR #21](https://github.com/pjotrcasteel/Argus/pull/21) merged to `main` at `cf58cc07eabd26251afded12e13e43ddb376f9f5`.
- [.NET CI 39029357971](https://github.com/pjotrcasteel/Argus/actions/runs/39029357971) passed **82/82 unit tests** and solution build, including deterministic direction vs timing, missing/unknown evidence, and zero-denominator tests.
- [External WyrmForge run 39029357952](https://github.com/pjotrcasteel/Argus/actions/runs/39029357952) succeeded at the existing limited visual-combat gate: 36 recorded frames, 21 inferred-player frames, 2 route clicks, 1 terminal stop. It created `persona-shadow.jsonl`, `persona-contrast.json` and `persona-contrast.md`; [short-lived artifact](https://github.com/pjotrcasteel/Argus/actions/runs/39029357952/artifacts/12099282857).
- Each persona proposed **22 actions**. Pairwise **movement-direction** disagreement when both were moving: conservative vs adventurer **10/19**, conservative vs daredevil **19/19**, adventurer vs daredevil **16/21**. Each pair had **21/22** full-action differences. Counts differ from denominators for waits/movement timing; see M2.1 report for definitions.
- **No independent replications or blind human labels** were used for these behavioral differences. A 19/19 contrast is **designed shadow policy divergence**, not a verified risk trait or 100% competence improvement. No alternative actions were executed, no game outcomes counterfactually observed, no learning occurred.

## Latest M2.2 cohort and controlled trait experiments (2026-10-17)

- [PR #22](https://github.com/pjotrcasteel/Argus/pull/22) merged to `main` at `d063c98ab786621f29654030a53c7a46fa3f7f70`.
- [.NET CI 39030722181](https://github.com/pjotrcasteel/Argus/actions/runs/39030722181) passed **92/92 deterministic MSTest tests**, solution build and a real invocation of the new `Argus.Persona.Cohort.Tool` with two separate **synthetic** trace fixtures. The first test attempt had one mistaken fixture where exploration couldn't happen under low confidence; it was corrected by testing attention and exploration under separate appropriate observations.
- [Real external WyrmForge visual smoke 39030722177](https://github.com/pjotrcasteel/Argus/actions/runs/39030722177) passed: **57 recorded frames**, 38 visually located player frames, 2 route clicks; 0 terminal stops. Three profiles each generated **41 offline hypothetical actions**. New `persona-ablation.json` was generated successfully on those genuinely recorded frames. The [short-lived artifact](https://github.com/pjotrcasteel/Argus/actions/runs/39030722177/artifacts/12100302475) holds screenshots and all reports.
- M2.2 **does NOT yet supply actual measured across-run WyrmForge data**: the cohort CLI was verified against artificial fixtures, and ablations were verified against one real trace. Neither demonstrates actual held-out accuracy, player realism, balanced combat, or learned AI.
- Non-ablated persona behavior remains unchanged: when no shared random-stream override is supplied, deterministic hashing still uses the persona ID. The new override is exclusively for controlled comparisons.

## Latest M2.3 independent REAL browser research (2026-10-17)

- [PR #23](https://github.com/pjotrcasteel/Argus/pull/23) merged into `main` at `9627b7f2818bb3b643dcedc0695b3d8490993077`. Dedicated Actions workflow remains **opt-in** except validation when its own file changes.
- [.NET CI 39031395741](https://github.com/pjotrcasteel/Argus/actions/runs/39031395741): existing solution build and tests passed.
- [Real two-session WyrmForge research run 39031395872](https://github.com/pjotrcasteel/Argus/actions/runs/39031395872) passed including the actual M2.2 multi-run cohort, with two sequential **separate Chromium processes**. Each completed 43 recorded frames, 30 frames visually classified as gameplay; **both visual-combat gates passed**.
- Read [short-lived artifact 12100298645](https://github.com/pjotrcasteel/Argus/actions/runs/39031395872/artifacts/12100298645): `run-01` had 2 gameplay frames with unknown candidate locations; `run-02` had 0. The raw manifests record process start/finish time, `runnerExitCode=0`, 390×844, same actual profile hash and policy SHA; game version/true outcome remain `null`/`unknown`. **MatchedContext=false** by design because the publicly observable game version was not machine-verified.
- Real-run pairwise shadow direction differences (numerator / frames where both proposed movement):
  - conservative vs adventurer: run-01 **15/25**, run-02 **19/25**
  - conservative vs daredevil: run-01 **25/25**, run-02 **24/25**
  - adventurer vs daredevil: run-01 **24/28**, run-02 **28/30**
- Distinct process executions and nonidentical trace bytes are genuine **separately captured** observations, but don't prove equal procedural worlds or conditions. Alternative persona actions were **never executed** and no enemy/player accuracy, actual wins, balancing quality or machine learning is established. These are descriptive designed-policy comparisons, not statistical evidence about human risk-taking.

## Latest M2.4 real-run blinded quality gate (2026-10-17)

- [PR #24](https://github.com/pjotrcasteel/Argus/pull/24) merged into `main` at `417e58470eafb0892f2f9aec7357e4b6925380de`.
- [.NET CI 39032328644](https://github.com/pjotrcasteel/Argus/actions/runs/39032328644) passed the full solution build and **97/97 MSTest tests**, including changed partitions, omitted/selected-frame tampering, synthetic verified-score plumbing, missing/all-unknown labels and zero-fabrication evaluation. Synthetic tests do not establish game accuracy.
- [Real bounded independent two-session research 39032328654](https://github.com/pjotrcasteel/Argus/actions/runs/39032328654) succeeded end-to-end; both real sessions passed their visual-smoke gates. M2.4 pre-registered 12 screenshots each using seed `argus-m2-4-first-blind-research`. `run-01` assigned **held-out**, `run-02` assigned **development**. **No annotations were invented.** Both `study-quality.json` and Markdown truthfully say `NOT_EVALUATED`, 0/12 human-reviewed player/threat frames per run, no quality percentages.
- The real ZIP [artifact 12100334875](https://github.com/pjotrcasteel/Argus/actions/runs/39032328654/artifacts/12100334875) includes 103 files (original screenshots/traces, `blind-annotate.html`, unknown templates, `study-plan.json`, `study-quality.json`/`study-quality.md`, persona cohort). Independently inspected ZIP contents and confirmed no serialized concrete player-coordinate predictions in either blind page. GitHub artifact retention is two days; the conversation may additionally contain a downloaded snapshot.
- [External baseline smoke 39032328655](https://github.com/pjotrcasteel/Argus/actions/runs/39032328655) also passed. Note that the workflow and UI cannot authenticate whether a later `labels.json` was really made by a human; **reviewer provenance and integrity are procedural requirements**.
- **Not achieved:** human-labeled actual precision/recall, trained AI, successful persona-controlled WyrmForge game outcomes, balancing conclusions or live spectator mode. Neither two browser sessions nor 12 sampled images per run supports strong statistical generalization.

## Current engineering judgement / active hypotheses

1. **Perception is the primary bottleneck, not learning algorithms yet.** Stabilize player identity and player-vs-effects-vs-scenery classification before training policies or doing statistical balancing. Image colors alone are fragile.
2. **Camera motion contaminates raw screen displacement.** Current M1.4 scene-shift is diagnostic only because particles, parallax, zoom and effects make it uncertain. Review against a labeled/held-out sample before allowing it to affect action selection.
3. **Static decorations can resemble enemies.** Candidate labels are intentionally uncertain. Multi-frame association and temporal consistency should reduce false positives, but require measurement.
4. **The first evidence gate is too weak for balancing.** Menu/route clicks + player frames can succeed without a meaningful win/loss. The M1.5 reporter addresses data integrity, denominators and comparability, but does NOT infer victory. Collect labeled outcomes, run manifests and held-out independent runs before using any metric to adjust balance.
5. **Replay before realtime streaming.** The user wants to watch one ARGUS run in real time later. The safest sequence is comparable run data → local read-only post-run replay → bounded localhost spectator (2–5 fps), always independent of game input and with no paid platform.
6. **Avoid hidden coupling to WyrmForge.** The first game-specific visible labels/palettes live in a profile, not in the core. New games get adapters/profiles, not integrations.
7. **The generated public game can change between runs.** Recheck on the real website; multiple procedural seeds and screen sizes matter. One screenshot or one lucky run is not evidence of robust play.

## Latest M1.8.1 browser UI validation (2026-10-17)

- [PR #18](https://github.com/pjotrcasteel/Argus/pull/18) merged onto `main` at `ad275710a4da8dd6721415d90797c9acb5406a19`, following the independently merged annotation baseline PR #15. Earlier duplicate PR #16 was closed without merge; no separate competing annotation system remains.
- [.NET CI 39026791218](https://github.com/pjotrcasteel/Argus/actions/runs/39026791218) passed **61/61 tests** and the full .NET solution build.
- [Real WyrmForge + Chromium UI 39026791236](https://github.com/pjotrcasteel/Argus/actions/runs/39026791236) passed. The agent's visual-combat smoke passed (40 inferred player frames, 2 route clicks). The separate headless browser loaded the generated real-game `annotate.html`, clicked manual player/threat points, downloaded `labels.json`, validated its schema and successfully **re-imported** it. These test labels were discarded, not counted as ground truth. [Short-lived screenshots + viewer artifact](https://github.com/pjotrcasteel/Argus/actions/runs/39026791236/artifacts/12098479280).
- New recorded threat coordinates can be drawn as **unverified predictions** in offline replay/annotation, visually distinct from manually confirmed positions. Legacy traces where `threatLocations` is `null` remain supported; unknown evidence is not silently converted to empty.
- **Not yet proven:** human ease-of-use, precision/recall against independently reviewed screens, machine learning, real-time spectator. Future work must acquire actual held-out labels.

## Single recommended next slice

**M2.5: real human-reviewed screenshot labels and first honest failure analysis**, without pretending to be a human reviewer or using detector outputs as labels.

1. Provide the owner a convenient copy of the M2.4 actual research artifact with `run-02/blind-annotate.html` (**development**) and `run-01/blind-annotate.html` (**held-out**). The user/reviewer can mark visible player center, threats, known empty, or unknown. If unsure, use **unknown**. Export `labels.json` into each corresponding run directory.
2. Prefer reviewing the **development** images first. Collect `run-01` held-out reference independently and avoid repeatedly tuning thresholds to it; after using a held-out sample for iterative decisions, collect a new untouched holdout.
3. Invoke `dotnet run --project src/Argus.Study.Quality.Tool -- --runs <extracted-research-folder>`; inspect per-run TP/FP/FN/TN, unknown/missing evidence and precision/recall. This evaluation currently is NOT_EVALUATED, not a real score.
4. If manual review is difficult on mobile, a future enhancement can provide a better independent standalone labeling experience; **do not substitute automated predictions for human truth**. Explicitly review particle/terrain lookalikes, absent vs occluded mage and camera-relative hazards.
5. Once detection is grounded and sufficiently reliable, design a *strictly bounded* persona-executing trial with ordinary game input and actual observed outcomes; do not call it trained AI. The user's Live Spectator Mode [issue #11](https://github.com/pjotrcasteel/Argus/issues/11) remains planned and unimplemented.
6. Keep M2.3 real capture opt-in to conserve GitHub Actions minutes. Avoid more automated research runs until annotation data is ready or the existing artifacts expire.

## Local usage

```bash
dotnet restore Argus.slnx
dotnet build Argus.slnx
dotnet test tests/Argus.Tests/Argus.Tests.csproj
dotnet run --project src/Argus.Reporter -- --runs results/experiments --output results/cohorts/demo
dotnet run --project src/Argus.Replay.Tool -- --run results/local-run
dotnet run --project src/Argus.Evaluation.Tool -- --run results/local-run --template results/local-run/labels.template.json
dotnet run --project src/Argus.Evaluation.Tool -- --run results/local-run --annotate results/local-run/annotate.html
dotnet run --project src/Argus.Evaluation.Tool -- --run results/local-run --blind-annotate results/local-run/blind-annotate.html
dotnet run --project src/Argus.Study.Tool -- --runs results/experiments --seed fixed-experiment-1 --samples 12
dotnet run --project src/Argus.Persona.Tool -- --run results/local-run --movement-keys w,a,s,d --seed 42
dotnet run --project src/Argus.Persona.Cohort.Tool -- --runs results/experiments --movement-keys w,a,s,d --seed 42
# writes persona-shadow.jsonl, persona-contrast.json, persona-contrast.md
pwsh src/Argus.Runner/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet run --project src/Argus.Runner -- --url https://pjotrcasteel.github.io/Wyrmforge/ --agent visual --profile examples/wyrmforge.profile.json --max-steps 56 --timeout-seconds 180 --output results/local-run
```

The local **visual** mode requires Tesseract installed. Screenshots and traces are Git-ignored. Do not upload or commit personal game sessions or secrets. The public GitHub Actions job operates only on publicly accessible/logged-out WyrmForge and produces short-lived artifacts.

## Continuity protocol — update this after each meaningful working slice

1. Check current `main`, open PRs/issues and latest Actions before choosing changes.
2. Create a focused branch, implement, test, run appropriate bounded live evidence if behavior changes.
3. Record **what changed**, PR/commit/CI links, measured evidence, limitations and exact next action in this file.
4. Update the README/roadmap when milestones change; keep `AGENTS.md` pointing here.
5. Merge only when build/tests and, for behavior changes, real visual evidence support the claim. Leave honest blockers in the PR and here.
6. Before answering the user, check that this file is actually on GitHub **main** and direct the user to its permalink.

### Status log

- **2026-10-17 — Baseline:** after merged PR #10: screenshot-only visual game prototype and motion diagnostics. Live run 39022431491 was green with the numbers above. Next: M1.5 offline multi-run cohort comparison.
- **2026-10-17 — M1.5 completed:** [PR #12](https://github.com/pjotrcasteel/Argus/pull/12) merged: offline validation, Markdown/JSON cohort reporting, controlled-manifest matching and technical-gate uncertainty. Unit tests and live smoke passed; latter had **0** inferred camera shifts.
- **2026-10-17 — M1.6 completed:** [PR #13](https://github.com/pjotrcasteel/Argus/pull/13) merged: safe offline replay generator, 48/48 tests and real `replay.html` from run 39024084743.
- **2026-10-17 — M1.7 instrumented:** [PR #14](https://github.com/pjotrcasteel/Argus/pull/14) merged: human-label schema, scored pixel metrics and actual threat locations in traces; 54/54 tests and a real all-unknown template.
- **2026-10-17 — M1.8 implemented:** [PR #15](https://github.com/pjotrcasteel/Argus/pull/15) merged: offline clickable annotation studio with per-frame labels and local JSON export, 58/58 tests, real WyrmForge HTML generation + JavaScript syntax check. **Not yet tested interactively by a human**; no manually verified reference dataset or WyrmForge precision/recall. **Next:** M1.9 small independent held-out annotation study; local Live Spectator [issue #11](https://github.com/pjotrcasteel/Argus/issues/11) remains future.
- **2026-10-17 — M1.8.1 browser-verified:** [PR #18](https://github.com/pjotrcasteel/Argus/pull/18) merged after 61/61 tests and real Chromium screenshot click/export/import success in run 39026791236. Still **no human-reviewed held-out detection accuracy**. Next M1.9 label real frames and score uncertainty.
- **2026-10-17 — M1.9 blind protocol complete:** [PR #19](https://github.com/pjotrcasteel/Argus/pull/19) merged, 68/68 tests and real WyrmForge blind-page generation. Development/test split is at the run level, not adjacent frames; no actual human labels yet. **Next:** independent annotation study plus game-neutral persona shadow policies.
- **2026-10-17 — M2.0 personality shadow policies:** [PR #20](https://github.com/pjotrcasteel/Argus/pull/20) merged, 75/75 tests, 49-frame real WyrmForge trace read with 33 hypothetical actions per preset. **No actions executed**, no accuracy/realism claims. **Next:** contrast metrics + independent human labels.
- **2026-10-17 — M2.1 quantitative persona comparisons:** [PR #21](https://github.com/pjotrcasteel/Argus/pull/21) merged, 82/82 tests and real 36-frame WyrmForge smoke. Conservative/daredevil varied direction on 19/19 mutually moving frames, but this is **one non-independent shadow trace, not verified human realism**. Next M2.2 multi-run/trait ablation and M1.9 manual held-out labels.
- **2026-10-17 — M2.2 independent-run cohort + trait ablation infrastructure:** [PR #22](https://github.com/pjotrcasteel/Argus/pull/22) merged, 92/92 tests, synthetic two-run full CLI verification and one 57-frame real WyrmForge ablation run. **No real WyrmForge two-run cohort exists yet.** Next M2.3 opt-in, bounded, real two-session collection and blind human labeling.
- **2026-10-17 — M2.3 real independent capture:** [PR #23](https://github.com/pjotrcasteel/Argus/pull/23) merged, Actions 39031395872 produced **two separate 43-frame real game traces**, both passed technical gates; pairwise conservative/daredevil direction contrast 25/25 and 24/25, **shadow only**. No human labels or verified game version/outcomes. Next M2.4 blind real-player/threat annotation gate.
- **2026-10-17 — M2.4 real blind ground-truth pipeline:** [PR #24](https://github.com/pjotrcasteel/Argus/pull/24) merged; 97/97 tests, true two-session research, 12 prespecified blind screenshot labels per session. Both run reports **NOT_EVALUATED** until a reviewer supplies actual labels. Next M2.5 collect original-image human labels and inspect real misdetections.

- **2026-10-17 — iPhone annotation fix pending one GitHub Pages setting:** [PR #25](https://github.com/pjotrcasteel/Argus/pull/25) merged to `main`, new `web/mobile-annotator` site includes exactly 12 real **development-run** screenshots and no held-out/agent data. [PR browser build 39040358391](https://github.com/pjotrcasteel/Argus/actions/runs/39040358391) passed real mobile Chromium image-load/navigation/tap/download/local-reload test. The [main publish run 39040444550](https://github.com/pjotrcasteel/Argus/actions/runs/39040444550) built successfully but its deploy job failed at `actions/configure-pages` because Pages isn't enabled. **User must open https://github.com/pjotrcasteel/Argus/settings/pages → Build and deployment → Source = GitHub Actions**. Then use GitHub connector `rerun_failed_workflow_run_jobs` on 39040444550 (or click Re-run failed jobs), verify deployment and only then share actual HTTPS URL `https://pjotrcasteel.github.io/Argus/annoteren/run-02/`. Do NOT claim the link works before deploy succeeds. Sandbox HTML previews on iPhone do not execute JavaScript; don't send more local HTML files as a workaround.
