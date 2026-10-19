#!/usr/bin/env python3
"""M2.43: append ONLY owner-approved ORIGINAL DEVELOPMENT blind reviewers to Pages.

Previous approved 324 original PNGs, 24 pages, frozen run-02 and older
cohorts remain byte-identical. NEW m238-a/b/c are 54 SHA-verified target
images only; no predecessors, private owner labels, traces, held-out O/P/Q,
game internals, or player predictions are copied to public.
Owner reconfirmed 2026-10-19 that these mobile annotation pages should be
published as with earlier development cohorts.
"""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

from tools.m238 import fresh_capture as m
from tools.m238.validate_archive import validate

CONTRACT = "M2.43_APPROVED_PUBLIC_M238_DEVELOPMENT_ONLY"
ROUTES = tuple(f"annoteren/{run}/" for run in m.RUNS)
STANDING_CONSENT = ("Owner explicitly reconfirmed on 2026-10-19 that ARGUS should "
                    "publish mobile screenshot annotation pages like earlier development "
                    "cohorts; ONLY original blank development targets, never owner labels, "
                    "previous context images or sealed/held-out sources.")


def require(ok, reason):
    if not ok:
        raise ValueError(reason)


def approved_base(root: Path):
    """Inspect the preexisting approved Pages source BEFORE any mutation."""
    require(root.is_dir() and not root.is_symlink(), "Missing prior approved Pages source.")
    original_index = root / "index.html"
    orig_manifest = root / "published-development-manifest.json"
    require(original_index.is_file() and orig_manifest.is_file() and
            (root / ".nojekyll").exists(), "Prior approved Pages baseline missing.")
    prior = json.loads(orig_manifest.read_text())
    require(prior.get("m232PointRoutes") ==
            [f"annoteren/dev-{run}/" for run in "uvw"] and
            prior.get("m232HumanTruth") == "ALL_UNKNOWN" and
            prior.get("m232SourceStudySeed") ==
            "argus-m232-independent-uvw-development-20261018-v1" and
            "standing consent" in prior.get("m215ReviewConsent", "") and
            len(list(root.glob("annoteren/*/frame-*.png"))) == 324 and
            len(list(root.glob("annoteren/*/index.html"))) == 24 and
            len(list(root.rglob("*.json"))) == 1 and
            not list(root.rglob("*.jsonl")) and
            not list(root.rglob("prev-for-*.png")) and
            not any((root / route).exists() for route in ROUTES),
            "Not the untouched 324-original, 24-page owner-approved development baseline.")
    return prior, original_index.read_bytes(), orig_manifest.read_bytes()


def publish(base: Path, sources: Path, review: Path):
    """Refuse unknown originals, then append 54 review screenshots only."""
    prior, original_index, _ = approved_base(base)
    source_gate = validate(sources, review)
    study = json.loads((review / "study-manifest.json").read_text())
    require(source_gate["originalTargets"] == 54 and
            source_gate["uniqueTargetDigests"] == 53 and
            source_gate["authenticPredecessors"] == 54 and
            study.get("ownerGroundTruth") == "NONE" and
            study.get("prospectiveSealedSourcesAccessed") is False and
            study.get("publicPublicationAuthorized") is False,
            "Only prior frozen DEV original-only evidence may be owner-approved.")

    added = {}
    for run, record in zip(m.RUNS, study["runs"]):
        folder = base / "annoteren" / run
        folder.mkdir(parents=True, exist_ok=False)
        original_html = (review / "annoteren" / run / "index.html").read_bytes()
        require(b"/^m238-[abc]$/" in original_html and
                b"fetch(" not in original_html and
                b"playerX" not in original_html and
                b"threatLocations" not in original_html and
                b"localStorage.setItem(storageKey" in original_html and
                b'"state":"visible"' not in original_html,
                "Owner reviewer unexpectedly contains guesses, network code or human labels.")
        (folder / "index.html").write_bytes(original_html)
        files = {}
        for entry in record["images"]:
            name = entry["file"]
            require(name.startswith("frame-") and name.endswith(".png") and
                    name not in files, "Not a frozen unique target frame filename.")
            raw = m.real_png(review / "annoteren" / run / name)
            require(m.sha(raw) == entry["sha256"], "Original review screenshot digest drift.")
            (folder / name).write_bytes(raw)
            files[name] = entry["sha256"]
        require(len(files) == 18, "Each new review must contain 18 original screenshots.")
        added[run] = files

    public_info = {
        "sourceRole": "NEW_REAL_ORIGINAL_DEVELOPMENT_SCREENSHOTS_BLIND_REVIEW_ONLY",
        "humanGroundTruth": "ALL_UNKNOWN",
        "publishedOriginalOnly": True,
        "sourceCaptureRun": "39242924416",
        "targetShaSeed": m.SEED,
        "routes": list(ROUTES),
        "screenshots": added,
        "consent": STANDING_CONSENT,
        "ownerLabelsPublished": False,
        "originalPredecessorsPublished": False,
        "originalFullBrowserTracesPublished": False,
        "sealedSourceAccess": False,
        "sourceImageDigestsUnique": 53,
    }
    prior["m238PublicReview"] = public_info
    (base / "published-development-manifest.json").write_text(
        json.dumps(prior, indent=2, sort_keys=True) + "\n")

    # Add new clearly titled menu entries without altering previous anchors.
    index = original_index.decode("utf-8")
    require(index.count("</ul>") == 1 and index.count("</body>") == 1,
            "Prior approved index HTML changed; refuse mismatched append.")
    links = "".join(f'<li><a href="{r}">M2.38 {r.split("/")[1]}: 18 nieuwe oorspronkelijke beelden beoordelen</a></li>'
                    for r in ROUTES)
    index = index.replace("</ul>", links + "</ul>")
    (base / "index.html").write_text(index)
    verify(base, original_index=original_index)
    return public_info


def verify(base: Path, original_index=None):
    """Never allow a missing, replaced or extra image/trace to reach Pages."""
    manifest = json.loads((base / "published-development-manifest.json").read_text())
    info = manifest.get("m238PublicReview", {})
    require(info.get("routes") == list(ROUTES) and
            info.get("targetShaSeed") == m.SEED and
            info.get("humanGroundTruth") == "ALL_UNKNOWN" and
            info.get("ownerLabelsPublished") is False and
            info.get("originalPredecessorsPublished") is False and
            info.get("sealedSourceAccess") is False and
            info.get("sourceImageDigestsUnique") == 53 and
            "2026-10-19" in info.get("consent", ""),
            "M2.38 public site has wrong approval or source manifest.")
    scripts = {}
    for run in m.RUNS:
        folder = base / "annoteren" / run
        allow = set(info["screenshots"][run]) | {"index.html"}
        require(set(p.name for p in folder.iterdir()) == allow,
                "Unexpected files in public M2.38 reviewer.")
        html = (folder / "index.html").read_text()
        require(run in html and "localStorage.setItem(storageKey" in html and
                "fetch(" not in html and "playerX" not in html and
                "threatLocations" not in html and
                "/*ARGUS_POINT_STUDY*/" not in html,
                "Public M2.38 reviewer contains active network/prognosis code.")
        scripts[run] = html.split("<script>",1)[1].split("</script>",1)[0]
        for name, digest in info["screenshots"][run].items():
            require(m.sha(m.real_png(folder / name)) == digest,
                    "Public screenshot does not match frozen original SHA.")
    require(len(list(base.glob("annoteren/*/frame-*.png"))) == 378 and
            len(list(base.glob("annoteren/*/index.html"))) == 27 and
            len(list(base.rglob("*.json"))) == 1 and
            not list(base.rglob("*.jsonl")) and
            not list(base.rglob("prev-for-*.png")) and
            not list(base.rglob("*labels*")) and
            not list(base.glob("annoteren/run-01/*")) and
            not list(base.glob("annoteren/dev-o/*")) and
            not list(base.glob("annoteren/dev-p/*")) and
            not list(base.glob("annoteren/dev-q/*")),
            "Unexpected original context, owner labels, sealed sources or changed public count.")
    index = (base / "index.html").read_text()
    require(all(f'href="{route}"' in index for route in ROUTES),
            "Public homepage missing new owner review navigation.")
    if original_index is not None:
        unchanged = index
        for route in ROUTES:
            fragment = (f'<li><a href="{route}">M2.38 {route.split("/")[1]}: '
                        '18 nieuwe oorspronkelijke beelden beoordelen</a></li>')
            unchanged = unchanged.replace(fragment, "")
        require(unchanged.encode("utf-8") == original_index,
                "Previous published review navigation was changed.")
    return {"contract": CONTRACT, "routes": list(ROUTES), "sourceFrames": 54,
            "uniqueOriginalDigests": 53, "oldOriginalFramesPreserved": 324,
            "oldReviewerPagesPreserved": 24,
            "originalPreviousScreenshotsPublished": 0,
            "ownerTruthPublished": False, "sealedHistoricalSourceOpened": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--site", required=True, type=Path)
    parser.add_argument("--sources", type=Path)
    parser.add_argument("--review", type=Path)
    parser.add_argument("--verify-only", action="store_true")
    args = parser.parse_args()
    if args.verify_only:
        print(json.dumps(verify(args.site), indent=2))
    else:
        require(args.sources is not None and args.review is not None,
                "Only real verified source + reviewer may enter site.")
        print(json.dumps(publish(args.site, args.sources, args.review), indent=2))


if __name__ == "__main__":
    main()
