#!/usr/bin/env python3
"""M2.42: create owner-only local review kit from SHA-verified M2.38 originals.

Never commit or publish the generated ZIP: original images are PRIVATE. The
result is a complete local mobile reviewer with a standalone stdlib server,
not a public Pages site, game runner, labeler, inference or trained model.
"""
import argparse
import io
import json
import zipfile
from pathlib import Path

from tools.m238.validate_archive import validate
from tools.m242.private_review_server import source_index

KIT_ROOT = "argus-m238-private-review"
SERVER_PATH = Path(__file__).with_name("private_review_server.py")
README = """ARGUS M2.42 — private mobile review (original M2.38 screenshots)
=================================================================

Do NOT publish this folder or upload these pictures to GitHub Pages.
There are 54 frozen original targets (53 distinct source hashes) from
three independent real browser sessions. This is NOT annotated ground truth.

Unzip to your personal computer and use Python 3.12+ (stdlib only):

  cd argus-m238-private-review
  python private_review_server.py

Open the printed URL on the same computer. To review with an iPhone on
YOUR OWN TRUSTED WIFI, first find the computer's LAN IPv4 address:

  Windows:  ipconfig
  Mac:      ipconfig getifaddr en0
  Linux:    hostname -I

Then start using that particular address (illustrative only):

  python private_review_server.py --lan --host 192.168.1.23

It prints a one-session randomized private link. Type/copy that URL into
Safari on your iPhone connected to the SAME trusted Wi-Fi. Do not forward
the link, use public Wi-Fi, or expose/forward router ports. HTTP on a LAN
is NOT encrypted; other machines with network-sniffing capability could
theoretically observe private images. Default localhost mode is safer.

The site is READ ONLY: no POST, no upload, no telemetry, no public Pages.
The image-position labels save locally in the browser and download to
the device ONLY after you press 'Exporteer eigen JSON'. Export from
EACH run (a/b/c), for a total of 3 separate JSON files.

VISIBLE: tap only the REAL playable mage center, not menu art.
ABSENT: certain no playable mage appears anywhere in this screenshot.
UNKNOWN: screenshot ambiguous, player obscured or uncertain. DO NOT GUESS.

IMPORTANT: do NOT delete your device/browser data before exporting
each file: in-browser annotations are only local to that browser.
If you change browsers/computers, use the reviewer's JSON import.

The private label filenames are:
  m238-a-player-points.json
  m238-b-player-points.json
  m238-c-player-points.json

Send those three actual human-authored exports back in ChatGPT so the
original byte-SHA audit can be run. The exports are reference data, NOT
a training result, observed win rate or proof that ARGUS knows the player.

The KIT includes original target screenshots, original immediate
predecessors and the unchanged original blank reviewer pages only.
The full original source traces and sealed historical O/P/Q are NEVER
included in this smaller owner-review kit. Preserve the separate full
source ZIP and original review ZIP privately for scientific verification.
"""


def pack(sources: Path, review: Path, output: Path):
    evidence = validate(sources, review)
    allowed = source_index(review)
    manifest = json.loads((review / "study-manifest.json").read_text())
    if output.exists() or output.resolve().is_relative_to(review.resolve()) or output.resolve().is_relative_to(sources.resolve()):
        raise ValueError("Refuse existing output or writing into original immutable input.")
    if evidence["originalTargets"] != 54 or len(allowed) != 58:
        raise ValueError("Source-validated private reviewer count mismatch.")
    # Ship only the 54 viewer originals, authenticated index pages, genuine
    # 54 predecessor contexts, frozen manifest, launcher and usage instructions.
    all_paths = sorted(allowed | {
        item["file"] for run in manifest["runs"] for item in run["immediatePriorOriginalFrames"]
    } | {"study-manifest.json"})
    if len(all_paths) != 113:
        raise ValueError("Unexpected frozen member count; never include labels or traces.")
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for name in all_paths:
            archive.writestr(f"{KIT_ROOT}/{name}", (review / name).read_bytes())
        archive.writestr(f"{KIT_ROOT}/private_review_server.py", SERVER_PATH.read_bytes())
        archive.writestr(f"{KIT_ROOT}/README-FIRST.txt", README.encode())
    with zipfile.ZipFile(output) as archive:
        names = archive.namelist()
        if len(names) != 115 or archive.testzip() is not None or len(set(names)) != 115 or any("label" in n.lower() or "trace.jsonl" in n.lower() for n in names):
            output.unlink(missing_ok=True)
            raise ValueError("Private reviewer ZIP included invalid or unsafe files.")
    return {"status": "PRIVATE_ORIGINAL_REVIEW_KIT_READY",
            "sourceRuns": 3, "originalTargets": 54,
            "originalImmediatePredecessors": 54,
            "uniqueTargetSha256": evidence["uniqueTargetDigests"],
            "containsOwnerTruth": False,
            "requiresExplicitLocalUserStart": True,
            "imagesPublishedToInternet": False}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--sources", type=Path, required=True)
    p.add_argument("--review", type=Path, required=True)
    p.add_argument("--output", type=Path, required=True)
    args = p.parse_args()
    print(json.dumps(pack(args.sources, args.review, args.output), indent=2))


if __name__ == "__main__":
    main()
