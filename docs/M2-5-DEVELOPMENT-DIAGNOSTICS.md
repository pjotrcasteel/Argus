# M2.5b — Human-reviewed perception failure analysis

Use **original, manually labeled** `labels.json` and the same preserved `trace.jsonl`/PNG frames. Never fabricate labels or use the detector's predictions as reference truth.

## Local workflow

From the ARGUS repository root, for the **development** run folder:

```bash
dotnet run --project src/Argus.Evaluation.Tool -- --run results/research/run-02 --labels results/research/run-02/labels.json
```

The tool now produces four separate local files in that run folder:

- `perception-report.json` and `perception-report.md`: original TP/FP/FN/TN/unknown metrics, excluding untrusted evidence.
- `perception-failures.json` and `perception-failures.md`: per-frame player status, one-to-one threat matches, human-marked missed centers, false-positive prediction centers, and likely geometric blind spots.

No data is uploaded by this CLI. The original run and labels are not modified. Screenshots, traces, and human-reviewed labels are excluded from this repository.

## Measurement rules

- **`NOT_OBSERVED`**: menu/transition screenshot without a combat-perception decision. Do not call a visible actor a detector false negative when detection did not run.
- **`UNKNOWN_REFERENCE`**: human explicitly did not label an object. Do not infer an empty scene.
- **`NO_PREDICTION_EVIDENCE`**: legacy trace lacks candidate positions. Do not treat it as zero predicted enemies.
- **`LEGACY_FINAL_UNVERIFIED`**: older runner reached its bounded final step with a null action, an exploring-phase decision, and no fresh-observation marker. M2.5a established that the older runner reused `LastEvidence` from the previous screenshot. Treat these player and enemy measurements as unknown rather than counting spurious hits/misses.
- **`SCORED`**: original per-frame predictions and original human labels are available. Matches are ordered by distance and assigned one-to-one at 26 pixels.
- **Outside old search region**: an unmatched manual center lies outside the historical color segmenter's x/y bounds (horizontal 1/30..29/30 and vertical 19%..82%). This is a **diagnostic correlation**, not proof that the geometry alone caused a miss.

A new M2.5a trace explicitly writes `inputSuppressedAtStepLimit` when its last screenshot has been freshly analyzed without executing the proposed final action. Such evidence is eligible for evaluation.

## Initial development finding: genuine run-02 human labels

The owner manually annotated 12 preselected frames in the actual M2.4 `run-02` development session. They were recovered from a prior conversation, **not committed to GitHub**. The original `run-01` held-out session remains untouched and unreviewed.

The former last screenshot (frame 42) contains a copied frame-41 prediction and must be excluded. Frame 26 is a transition without an attempted combat observation. With these reliability restrictions, seven combat frames are comparable. An independent implementation of the original 26-pixel nearest-neighbor rules found **27 true-positive, 2 false-positive, and 11 false-negative enemy centers**: development precision 27/29 (93.1%), recall 27/38 (71.1%). Six of eleven missed centers are outside the old detector scan geometry. The official .NET evaluator remains the source of truth; do not claim a new model performance improvement from code that only improves accounting.

Expanding all detection bounds naively generated many additional candidate artifacts in an exploratory offline probe, so do not modify active enemy avoidance thresholds from these seven development frames alone. Test changes against new independently captured development evidence before using a fresh untouched held-out run.

## Next

Validate the .NET regression tests and bounded real-browser smoke, then run the official CLI on the **private recovered labels + preserved original evidence**. This is a diagnostic-only slice; current active gameplay behavior is unchanged. Afterward choose a separately testable detector experiment with explicit false-positive controls.