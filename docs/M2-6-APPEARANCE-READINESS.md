# ARGUS M2.6e — Do we have enough human references to explore a learned candidate scorer?

## What this milestone does

`AppearanceReadiness` performs an **offline reference sufficiency audit** over development-run
`appearance-audit.json` files. It is not a classifier and does not train or deploy any model.
Its input is only the already-scored visual candidates versus independent human-reviewed
development screenshot centers. "Matched" means within the matching radius of a human
mark, **not** a verified sprite identity or enemy category. A missed ground-truth actor
not represented by a detector candidate is still a false negative, **not** a negative
training example. Never infer that an unlabeled object is decoration.

This component explicitly requires:

- At least **three** distinct development-run IDs for offline exploration, checked at the
  run level rather than splitting adjacent screenshots into fake independent data.
- Within **each** run: at least five scored screenshots, ten matched candidate positions,
  and ten unmatched candidate positions to avoid extremely undersampled negative/positive classes.
- Consistent per-frame counts and candidate totals. Tampered files or duplicate run identities
  cause an exception instead of silently providing a misleading green result.

Even `SUFFICIENT_FOR_OFFLINE_EXPLORATION` does **not** authorize training on a held-out
reference, calling model predictions verified enemy identities, changing live WyrmForge
steering or claiming cross-game generalization. More data and separate blind evaluation
are needed for that.

## Genuine private development evidence, 2026-10-17

The owner manually labeled 12 presampled screenshots each in dev-a and dev-b.
The existing independent Python-based C# pixel-feature reproduction found:

| Run | Scored combat frames | Matched human centers | Unmatched candidate centers | Unmatched human threat centers |
| --- | ---: | ---: | ---: | ---: |
| dev-a | 6 | 30 | 16 | 6 |
| dev-b | 9 | 43 | **2** | 14 |
| Total | 15 | 73 | 18 | 20 |

The 73/18/20 TP/FP/FN is **raw shadow** from Python mirror, not the official .NET CLI.
The old active baseline across those screenshots was 58/15/35. Since each row belongs
to only **one** development run, the total 91 candidates are emphatically **not** 91
independent sessions.

A lightweight exploratory private Python logistic probe trained on the dev-a descriptors
and evaluated on dev-b. At a **fixed 0.5 threshold**, it got candidate agreement
42 TP / 1 FP / 15 FN, versus raw shadow 43/2/14; there was no F1 improvement.
The reverse direction, trained on dev-b, obtained 26/0/10 on dev-a versus raw 30/16/6,
but **dev-b had only two negative examples**, so this apparent high test precision is
not credible model validation. Both runs are development data and the original run-01
held-out set was untouched. **No trained model or fitted weights were committed.**

The evidence sufficiency gate should therefore return `NOT_READY` on these two
owner-reviewed development runs, indicating **two** distinct runs instead of three and
**only two unmatched candidates** in dev-b.

## Execute locally with genuine labels (not on public CI)

First copy the genuine `dev-a/labels.json` and `dev-b/labels.json` supplied by the
owner beside their original local `trace.jsonl` and screenshot frames. Run the production
offline appearance audits (after M2.6d causal tracking landed in main):

```bash
dotnet run --project src/Argus.Evaluation.Tool -- --run results/m26b-private/dev-a --appearance-audit examples/wyrmforge.profile.json
dotnet run --project src/Argus.Evaluation.Tool -- --run results/m26b-private/dev-b --appearance-audit examples/wyrmforge.profile.json
dotnet run --project src/Argus.Study.Quality.Tool -- --runs results/m26b-private --appearance-readiness
```

The last command also produces the existing `study-quality.json` and adds
`appearance-readiness.json` and `appearance-readiness.md` to the **private local**
study root. Only development partitions are inspected for visual feature sufficiency.
The official .NET run on the private human labels has not yet been executed by the
assistant because that local runtime lacks a .NET 10 SDK and the labels must not
be uploaded to the public Actions runner. Synthetic CI exercises the exact C# APIs,
including the complete read-only CLI, with **artificial** screenshots/labels only.

## Priority after this gate

Record at least one more **independent development session**, ideally with multiple
distinct combat backgrounds, particles, camera positions, and both real enemies and
non-enemy palette lookalikes. The owner should blind-label genuine original PNGs before
examining candidate predictions. Focus on informative false-positive regions without
manually cherry-picking only easy negatives. A truly untouched *new* held-out run
should then test any frozen offline scorer once, after research/tuning is done.
