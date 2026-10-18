# M2.26 — Preregistered unclipped image-wide contrast and out-of-fit human-ABSENT calibration

## WHY (negative M2.25 evidence; do not relabel it as success)

The M2.25 **original** 246-feature spatial RGB/HOG candidate model, when
calibrated on its own fit-run whole-ABSENT frames using max probability +
0.025 capped at 1.0, saturated at threshold **1.0** in all three
source-run-out folds. A strict `probability > 1` decision can never fire:
**0/36 visible actual players located; 18/18 absent frames rejected
trivially**. Center-always-present got 23/36 real positions but false
presence on every one of 18 human-ABSENT frames. This is a NO-GO.

The M2.26 candidate was designed knowing this **development-only**
failure. Accordingly, any evaluation on previously seen L/M/N or
I/J/K remains exploratory. **NEVER** imply independent generalization
or selectively tune the method after seeing those results.

## Frozen new hypothesis, before M2.26 evaluation

`tools/m226/framewise_conformal.py` reuses, without modifications,
the exact M2.23 preregistered original 64×64, 246-feature HOG/RGB
representation, 20px fixed image-wide scan, class-balanced logistic
training and candidate patch rules. However, it replaces the broken
probability>1 image gate with this single fixed protocol:

1. For each left-out complete development run, remove byte-identical
   original PNGs from the other two training runs by SHA256. Never
   see any held-out labels in fitting/calibration.
2. Deduplicate training screenshot SHA values, then per remaining
   whole training run assign every *chronologically alternate distinct
   human-ABSENT screenshot* to the calibration subset (indices 0,2,4,...)
   and the remaining ABSENT images + all human-VISIBLE images to fitting.
   Reject if fewer than three true whole ABSENT images remain in fitting
   or fewer than **four distinct out-of-fit whole ABSENT frames** remain
   in calibration. Human-UNKNOWN contributes nothing.
3. Fit the unchanged M2.23 regularized 246D patch classifier using
   **only fitting** images. Do not let any calibration frame influence
   its coefficients or feature-normalization moments. The old M2.23
   sigmoid threshold is not used for M2.26 decisions.
4. For each full 390×844 image, compute the **UNCLIPPED linear logit**
   of every candidate crop over the fixed 20px screen-wide grid. Define
   exactly one whole-frame statistic:
   `max(raw logits) - percentile(raw logits, 95)`.
   Candidate location is the grid point of the highest original logit.
   This contrast, rather than clipped sigmoid probability, avoids
   rounding all screen maxima to 1.0 and measures whether any region
   unusually stands out *within that screenshot*. No player-location
   prior, fixed player RGB palette, game code/DOM or game-derived labels.
5. On only the out-of-fit true whole-ABSENT calibration screenshots,
   calculate the same **entire-screen maximum candidate contrast**.
   At test time compute the conservative empirical upper-tail rank:
   `p = (1 + count(absent contrasts >= observed contrast)) / (1 + n_cal)`.
   **Equal scores count as non-evidence**. Declare a player only when
   `p <= 0.20`, otherwise output ABSENT. This uses actual
   negative **whole frames**, not any one background patch as if it were
   an independent negative frame. The finite resolution is
   `1/(n_cal+1)`; fewer than four true absent calibration screens
   means an automatic NOT_READY, not relaxed threshold selection.
6. A player is counted truly localized only within **35 original pixels**
   of the human-visible center. Score presence false positives on
   *entire* truly absent screenshots separately from location.
   Compare exact same unseen whole source run to center-always-present.
7. Prespecified minimum evidence to even consider locking a research
   candidate: **at least 50% correct localization recall on VISIBLE
   human screens and at most 25% false player presence on ABSENT
   screens in EACH of the three left-out development runs**. If any
   fails, **NO-GO**; do not change alpha/quantile/weights on the
   withheld development-fold results or claim validated improvement.
8. Fit a separate development-only final image model+absence calibration
   on all three L/M/N development runs using identical frozen rules;
   store exact normalization, coefficients, calibration scores, sample
   original SHA identities and canonical SHA256 **in a private local
   report only**. This is a reproducibility lock, not automatically
   permission to unseal prospective O/P/Q.

## Statistical caution

The empirical rank is a **conformal-style diagnostic, NOT a calibrated
probability that a player exists**. Classical marginal false-positive
guarantees depend on exchangeable independent absent screens; adjacent
frames in each game run, repeated menus, and domain drift violate
those assumptions. n=4–9 calibration frames gives very coarse rank
resolution and no validated protection against rare false positives.
The synthetic tests prove code invariants and correct abstention,
not performance on WyrmForge or a different game.

Any L/M/N result is retrospective development-only evidence. Only a
prospectively frozen, untouched independent multi-run cohort with
separate blinded human truth could support a real performance claim.
**Keep original O/P/Q sealed until model parameters and go/no-go
decision are established without any prospective pixels or labels.**
If current development evidence remains weak, preserve O/P/Q unopened
and collect genuinely new development evidence first.

## Private-only execution

With verified original development ZIP and three private human exports:

~~~bash
OPENBLAS_NUM_THREADS=1 python3 tools/m226/framewise_conformal.py \
  --archive /private/ARGUS_M223_54_Original_Development_Blind_Review.zip \
  --labels-l /private/m223-dev-l-player-points.json \
  --labels-m /private/m223-dev-m-player-points.json \
  --labels-n /private/m223-dev-n-player-points.json \
  --output /private/ARGUS_M226_Development_Exploratory.json
~~~

This command MUST NOT be called by GitHub Actions with real private
source/labels or push outputs into GitHub/Pages. The original sealed
O/P/Q ZIP and original M2.4 run-01 remain untouched; no game agent
policy or game source changes.
