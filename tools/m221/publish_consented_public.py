#!/usr/bin/env python3
"""M2.21: publish exactly the newly consented ORIGINAL M2.20 blind reviewers.

This never publishes trace JSONL, run outcomes, human labels, private owner
exports, original M2.4 held-out frames or fitted model weights.
The frozen research plan's 'publicPublicationAuthorized:false' was written
BEFORE the owner authorized later PUBLIC REVIEW on 2026-10-18; retain
that immutable original research truth and record new consent separately.
"""
import argparse
import hashlib
import json
import re
import struct
import sys
from pathlib import Path
from zipfile import ZipFile

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from tools.m219.preserve_public_site import check_site as check_original_public_site

SOURCE_ZIP_SHA256 = "a34a268537c500bef7f17c24c5721ad6308d6bd8c2f61634c7f04378a2ee4036"
SOURCE_TREE_SHA256 = "aec60332d41855b83b5f3ad7b883dafa72b96a116f6926b9b61e12edf4d7fcbc"
PUBLIC_SITE_TREE_SHA256 = "b2a8212286370191f982f2ff0811d51228bfbb6f40c8c6029711f4a1f3a44207"
SEED = "argus-m220-independent-blind-player-point-20261018-v1"
RUNS = ("dev-i", "dev-j", "dev-k")
EXPECTED_OLD_FILE_COUNT = 123  # 122 original published files + empty .nojekyll
EXPECTED_NEW_FILE_COUNT = 180  # 123 + exactly 3 blank HTML and 54 authentic PNGs
PNG_MAGIC = b"\x89PNG\r\n\x1a\n"


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def require(ok, message):
    if not ok:
        raise ValueError(message)


def members(source):
    require(source.is_file() and source.stat().st_size < 20_000_000,
            "Missing or oversized owner-approved blank original-review ZIP.")
    require(digest(source.read_bytes()) == SOURCE_ZIP_SHA256,
            "Original 54-frame frozen development review ZIP changed.")
    with ZipFile(source) as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)) == 59, "Only 59 original blind-review entries allowed.")
        require(all(not n.startswith("/") and ".." not in Path(n).parts and not n.endswith("/")
                    for n in names), "Unexpected ZIP path or directory.")
        tree = digest("".join(n + "\x00" + digest(archive.read(n)) + "\n"
                              for n in sorted(names)).encode())
        require(tree == SOURCE_TREE_SHA256 and archive.testzip() is None,
                "Frozen source entry bytes or archive integrity differ.")
        plan = json.loads(archive.read("m220-study-plan.json"))
        require(plan.get("schemaVersion") == 1 and plan.get("studySeed") == SEED and
                plan.get("mode") == "development-only" and plan.get("runIds") == list(RUNS) and
                plan.get("humanTruth") == "ALL_UNKNOWN" and
                plan.get("publicPublicationAuthorized") is False,
                "Wrong original consent state, run or study seed.")
        expected = {"index.html", "m220-study-plan.json"}
        copied = {}
        hashes = {}
        for item in plan["runs"]:
            run = item.get("runId")
            require(run in RUNS and item.get("partition") == "development" and
                    item.get("humanTruth") == "ALL_UNKNOWN" and len(item.get("images", [])) == 18,
                    "Run does not contain 18 original blind development images.")
            html_name = f"annoteren/{run}/index.html"
            expected.add(html_name)
            html = archive.read(html_name).decode("utf-8")
            require(html.count("<script>") == html.count("</script>") == 1 and
                    "const study=" in html and "localStorage.setItem(storageKey" in html and
                    '"argus-m220-player-point-"' in html and
                    "fetch(" not in html and "playerX" not in html and
                    "threatLocations" not in html and "run-01" not in html and
                    '"state":"visible"' not in html and
                    "study.images.length!==18" in html,
                    "Public review HTML is not a blank private-label-only mobile reviewer.")
            study = re.search(r"const study=(\{.*?\});\s*const W=", html, re.S)
            require(study is not None, "Cannot validate embedded frozen page source.")
            page_data = json.loads(study.group(1))
            require(page_data == {"schemaVersion": 1, "seed": SEED, "sourceSeed": SEED,
                                  "runId": run, "partition": "development", "images": item["images"]},
                    "Blank reviewer frame list differs from immutable original research manifest.")
            copied[html_name] = archive.read(html_name)
            frame_hashes = {}
            for im in item["images"]:
                step = im.get("step")
                name = im.get("file")
                require(type(step) is int and step >= 0 and name == f"frame-{step:04d}.png" and
                        re.fullmatch(r"[a-f0-9]{64}", im.get("sha256", "")),
                        "Invalid original screenshot reference.")
                path = f"annoteren/{run}/{name}"
                expected.add(path)
                png = archive.read(path)
                require(24 <= len(png) <= 2_000_000 and png.startswith(PNG_MAGIC) and
                        struct.unpack(">II", png[16:24]) == (390, 844) and
                        digest(png) == im["sha256"], "Authentic original pixel SHA/dimensions differ.")
                frame_hashes[name] = im["sha256"]
                copied[path] = png
            require(len(frame_hashes) == 18, "Duplicate original sampled steps.")
            hashes[run] = frame_hashes
        require(set(names) == expected, "Unexpected raw trace, owner JSON, held-out or extraneous source file.")
        require(set(hashes) == set(RUNS) and len(copied) == 57,
                "Expected three approved blank reviewers and 54 PNGs only.")
    return copied, hashes


def build(snapshot, archive, output):
    check_original_public_site(snapshot)
    require(not output.exists() or not any(output.iterdir()),
            "Never replace pre-existing output, checkpoint or owner labels.")
    copied, hashes = members(archive)  # ALL SHA and privacy checks before publishing anything.
    old = {p.relative_to(snapshot).as_posix(): p.read_bytes()
           for p in snapshot.rglob("*") if p.is_file() and not p.is_symlink()}
    require(len(old) == EXPECTED_OLD_FILE_COUNT, "Original approved checkpoint has wrong file count.")
    require("m220-study-plan.json" not in old and
            all(not (snapshot / "annoteren" / run).exists() for run in RUNS),
            "Refuse to overwrite another existing public study.")
    target = Path(output)
    for name, raw in old.items():
        destination = target / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(raw)
    for name, raw in copied.items():
        destination = target / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(raw)
    manifest_path = target / "published-development-manifest.json"
    manifest = json.loads(manifest_path.read_text())
    require("m220PointRoutes" not in manifest and "m220Screenshots" not in manifest,
            "Previously published M2.20 content may not be overwritten.")
    manifest["m220PointRoutes"] = [f"annoteren/{run}/" for run in RUNS]
    manifest["m220SourceStudySeed"] = SEED
    manifest["m220ReviewConsent"] = "owner-confirmed prior standing permission for original blank reviewer publication 2026-10-18"
    manifest["m220Screenshots"] = hashes
    manifest["m220HumanTruth"] = "ALL_UNKNOWN"
    manifest_path.write_text(json.dumps(manifest, sort_keys=True, indent=2) + "\n")
    home = target / "index.html"
    html = home.read_text(encoding="utf-8")
    require(html.count("</ul>") == 1, "Cannot safely add three links to existing approved homepage.")
    links = "".join(f'<li><a href="annoteren/{run}/">M2.20 {run}: echte spelerlocatie aanwijzen (18 beelden)</a></li>'
                    for run in RUNS)
    home.write_text(html.replace("</ul>", links + "</ul>", 1), encoding="utf-8")
    verify(target, snapshot, copied, hashes)
    print("PASS: 54 explicitly owner-approved original PNGs + 3 wholly blank reviewer pages. Zero human labels or traces.")


def verify(site, old_snapshot=None, admitted=None, hashes=None):
    require(site.is_dir(), "New site missing.")
    files = {p.relative_to(site).as_posix(): p.read_bytes()
             for p in site.rglob("*") if p.is_file() and not p.is_symlink()}
    require(len(files) == EXPECTED_NEW_FILE_COUNT, "Unapproved new public file detected.")
    require(files.get(".nojekyll") == b"", "Missing original nojekyll marker.")
    manifest = json.loads(files["published-development-manifest.json"])
    require(manifest.get("m220PointRoutes") == [f"annoteren/{r}/" for r in RUNS] and
            manifest.get("m220SourceStudySeed") == SEED and
            manifest.get("m220HumanTruth") == "ALL_UNKNOWN" and
            set(manifest.get("m220Screenshots", {})) == set(RUNS),
            "Published manifest differs from exact newly consented reviewers.")
    allow = {".nojekyll", "index.html", "published-development-manifest.json"}
    allow.update(f"annoteren/{r}/index.html" for r in
                 ("run-02", "dev-a", "dev-b", "dev-c", "dev-d", "dev-e", "dev-f",
                  "dev-g", "dev-h", "dev-f-points", "dev-g-points", "dev-h-points", *RUNS))
    for run in ("run-02", "dev-a", "dev-b", "dev-c", "dev-d", "dev-e", "dev-f", "dev-g", "dev-h"):
        allow.update(f"annoteren/{run}/{x}" for x in
                     (p.name for p in (site / "annoteren" / run).glob("frame-*.png")))
    for run in RUNS:
        images = manifest["m220Screenshots"][run]
        require(len(images) == 18, "New public run has the wrong number of original PNGs.")
        for filename, sha in images.items():
            require(re.fullmatch(r"frame-\d{4}\.png", filename) and
                    digest(files.get(f"annoteren/{run}/{filename}", b"")) == sha,
                    "Original M2.20 review PNG hash differs.")
            allow.add(f"annoteren/{run}/{filename}")
        html = files[f"annoteren/{run}/index.html"].decode()
        require("localStorage.setItem(storageKey" in html and "fetch(" not in html and
                "run-01" not in html and '"state":"visible"' not in html,
                "Public review contains unapproved human truth or network upload.")
    require(set(files) == allow, "Public site contains unexpected private/held-out/model files.")
    if old_snapshot is not None:
        old = {p.relative_to(old_snapshot).as_posix(): p.read_bytes()
               for p in old_snapshot.rglob("*") if p.is_file() and not p.is_symlink()}
        for name, raw in old.items():
            if name not in ("index.html", "published-development-manifest.json"):
                require(files.get(name) == raw, "Existing approved screenshot or reviewer HTML changed.")
    if admitted is not None:
        require(all(files.get(name) == raw for name, raw in admitted.items()),
                "Source original new reviewer HTML/PNG bytes changed.")
    tree = digest("".join(name + "\x00" + digest(raw) + "\n"
                          for name, raw in sorted(files.items())).encode())
    require(tree == PUBLIC_SITE_TREE_SHA256,
            "Public checkpoint bytes differ from approved M2.21 PR #54 immutable site fingerprint.")
    print("PASS: exact pinned 180-file checkpoint, 162 PNGs and 15 blank reviewers; no private data.")
    return tree


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="mode", required=True)
    b = sub.add_parser("build")
    b.add_argument("--base", type=Path, required=True)
    b.add_argument("--archive", type=Path, required=True)
    b.add_argument("--output", type=Path, required=True)
    v = sub.add_parser("verify")
    v.add_argument("--site", type=Path, required=True)
    args = parser.parse_args()
    if args.mode == "build":
        build(args.base, args.archive, args.output)
    else:
        print("FULL_TREE_SHA256=" + verify(args.site))


if __name__ == "__main__":
    main()
