# M2.31 — private failure atlas for the **unchanged** M2.30 image learner

## Purpose

M2.30 fit and held-run-out verification was honest **NO-GO**. It is crucial
to distinguish its abstention errors from actually ranking a different image
region above the true player. Both can produce zero correct-player output,
but require different experimental hypotheses. An incorrect attempt to
"repair" this by relaxing the M2.30 threshold after reading its owner-label
results would overfit already inspected development evidence.

This standalone **read-only** offline tool reproduces the exact frozen
M2.30 train/calibration split (and refuses insufficient genuine ABSENT
controls), then examines only original SHA-verified, privately
human-reviewed R/S/T images. It does **not** retrain with newly selected
settings, alter the 0.05 absence margin, change grid/descriptor/weights,
or execute player input.

## Failure taxonomy

- `located`: model accepted a source-only top image location within
  35 original pixels of the human-visible player;
- `abstained_visible`: player is human-visible, but model rejected the
  entire screenshot as absent at its frozen out-of-fit threshold;
- `wrong_location`: model accepted a location more than 35px away from
  the verified player;
- `correctly_absent`: human-ABSENT and model refuses player presence;
- `absent_false_candidate`: human-ABSENT, but model claims any presence.

For **abstained_visible**, separately report whether the model's unaccepted
highest-scoring raw image patch was already within 35px of the true player.
This is *diagnostic candidate ranking*, **not** a successful model detection.
Report best-logit minus fixed original absence threshold and best-logit
minus true-player-patch logit only in the private local report. Never
rewrite a rejected candidate as positive merely because it was near the
owner's mark.

`unknown` source frames are excluded, never converted to absent.
Duplicate original screenshot SHA observations are deduplicated identically
to M2.30 and cannot inflate independent hit counts.

## Private command

Use authentic M2.27 113-member original R/S/T ZIP, exact original source
SHA256 `bdb8538c90ccb5166fcee4b94e9f46e6c4ba1a91d70079c370c4c13d65173bda`,
and the user's three original `m227-dev-{r,s,t}-player-points.json`
exports **locally**, never in GitHub.

```bash
python -m pip install "numpy>=2,<3" "Pillow>=11,<13"
OPENBLAS_NUM_THREADS=1 PYTHONPATH=. python tools/m231/private_identity_failures.py \
  --original-archive /private/argus_m227_original_pairs_private.zip \
  --labels-dir /private/argus-owner-reviews \
  --output-dir /private/m231-diagnostics \
  --render-private-overlays
```

The output folder must NOT already exist and may contain original game PNGs
annotated with owner reference center (magenta) and model's **raw** highest
score coordinate (amber). They are *not* approved for public screenshot
publication; never add the folder to source control, CI, Pages or a PR.
The report also contains original source SHA, frame steps, owner marks,
model evidence and private per-frame classification. No file upload or network
communication is part of this tool.

## Outcome from authentic development originals

The owner-private original source was revalidated and the M2.30 fixed
model was **reproduced**, with the same no-go metrics. The most frequent
error was genuine human-visible player **abstention**, including instances
where the highest raw patch lay close to the human point but remained below
the three-whole-image ABSENT calibration threshold. Other frames favored
non-player regions and there was also a rare actual ABSENT false presence.
Therefore **both** identity ranking AND presence rejection need
new experiments; merely lowering the currently frozen cutoff is not
a rigorous solution.

The private counts, screenshots, owner coordinates, marked PNGs and
logit gaps stay with the owner, not in this repository. Public CI exercises
synthetic classification cases only.

## Next research priority: M2.32

Formally preregister a replacement confidence/rejection mechanism tied
to actual source run independence and hard-negative whole-ABSENT frames,
while retaining explicit local shape identity evidence. Then collect
**new independent development source runs** and new owner-blind review
before choosing new thresholds. Keep original R/S/T as *seen development*
and never relabel it as prospective evidence. Preserve the **sealed**
original O/P/Q and M2.4 run-01 without opening, using local private
offline backups before expiry of original Actions artifacts.
Do not change the live ARGUS player tracker until independent player
location + whole-screen absence validation passes precommitted gates.
