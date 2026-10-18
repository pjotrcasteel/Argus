# M2.17 — Private original-reference adjudication

The owner confirmed the fixed M2.15 yellow circles are round on iPhone. Previously
completed labels still require human re-review if they contradict one another:
two spatially disjoint probes in the SAME screenshot cannot both contain the
one player. Never silently change a label from PRESENT to ABSENT.

## The new tool

tools/m217/reference_audit.py performs a local-only audit:
- Verify the pinned exact SHA256 of the authentic M2.15 ZIP, all 36 original
  screenshot hashes and 390x844 dimensions, source seed, and F/G/H run identities.
- Validate all 72 human labels against frozen locations, radius and roles.
- Reject unknown/held-out run IDs, moved candidates and unregistered evidence.
- Detect conflicting double-present pairs, without deciding which answer is wrong.
- For each conflict, produce a private, self-contained offline HTML viewer
  showing the two candidate circles **separately**, not on top of one another.
- Do not allow exporting a new JSON file until a human explicitly resolves
  each physical contradiction by selecting PRESENT, ABSENT or UNKNOWN.
- Preserve all unchallenged human labels and all original owner files unmodified.
- Never fit a model, contact a server or read original held-out run-01.

## Local CLI only

~~~bash
python3 tools/m217/reference_audit.py \
  --archive /private/M215-authentic-source.zip \
  --labels-f /private/m215-dev-f-labels.json \
  --labels-g /private/m215-dev-g-labels.json \
  --labels-h /private/m215-dev-h-labels.json \
  --output /private/m217-review
~~~

Read private-reference-audit.json. If it reports REQUIRES_HUMAN_REVIEW,
open the newly generated private-recheck.html LOCALLY. It contains original
game screenshots and completed human annotations; do NOT publish it.

After resolving conflicts, download the new corrected label file, rerun
the audit with the corrected file and the other two original exports, and
only then run M2.16 .NET 10 privately:

~~~bash
dotnet run --project src/Argus.Learning.Tool -- \
  --spatial-archive /private/M215-authentic-source.zip \
  --labels-f /private/m215-dev-f-labels-rechecked.json \
  --labels-g /private/m215-dev-g-labels.json \
  --labels-h /private/m215-dev-h-labels.json \
  --output /private/m216-results
~~~

REFERENCE_CONSISTENT establishes only *structural/physical consistency*,
not accuracy of individual owner answers. Unrecognized human mistakes may
remain after the audit. M2.16's whole-development-run results remain
exploratory, not prospective AI player detection or policy learning.

## Public CI boundary

Only synthetic fake labels and PNG headers are used in automated tests.
Mobile Chromium exercises 320/390/430 widths, marker geometry, export
blocking, explicit human review and nonmutation of original label bytes.
No owner labels, original research images or fitted weights are committed,
published to GitHub Pages, or uploaded as CI artifacts.
