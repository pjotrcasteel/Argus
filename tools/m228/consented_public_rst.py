#!/usr/bin/env python3
"""M2.28: publish ONLY consented dev-R/S/T original targets and BLANK reviewers.

Authentic preceding source context (54 PNGs), source traces, private owner
answers, prospectives O/P/Q and M2.4 run-01 must NEVER enter GitHub Pages.
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
from tools.m224.publish_development_only import verify as verify_previous_checkpoint

ARCHIVE_SHA256 = "bdb8538c90ccb5166fcee4b94e9f46e6c4ba1a91d70079c370c4c13d65173bda"
SITE_TREE_SHA256 = "PIN_AUTHENTIC_PUBLIC_TREE_AFTER_VERIFIED_BUILD"
SOURCE_SEED = "argus-m227-temporal-context-rst-20261018-v1"
RUNS = ("dev-r", "dev-s", "dev-t")
PNG = b"\x89PNG\r\n\x1a\n"


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def require(ok, message):
    if not ok:
        raise ValueError(message)


def read_original(archive):
    require(archive.is_file() and archive.stat().st_size < 30_000_000 and
            digest(archive.read_bytes()) == ARCHIVE_SHA256,
            "Wrong owner-approved authentic M2.27 original-development ZIP checksum.")
    with ZipFile(archive) as z:
        names = z.namelist()
        require(len(names) == len(set(names)) == 113 and
                all(not n.startswith("/") and ".." not in Path(n).parts and
                    not n.endswith("/") for n in names) and z.testzip() is None,
                "Unexpected private/held-out member or ZIP corruption.")
        plan = json.loads(z.read("study-manifest.json"))
        require(plan.get("schemaVersion") == 1 and plan.get("seed") == SOURCE_SEED and
                plan.get("runIds") == list(RUNS) and plan.get("targetsPerRun") == 18 and
                plan.get("originalTargetImages") == 54 and
                plan.get("originalPrecedingContextImages") == 54 and
                plan.get("humanGroundTruth") == "ALL_UNKNOWN" and
                plan.get("trainingAuthorized") is True and
                plan.get("prospectiveOriginalsAccessed") is False and
                len(plan.get("runs", [])) == 3,
                "Wrong frozen independent development/temporal source identity.")
        expected = {"study-manifest.json", "index.html"}
        published = {}
        hashes = {}
        for source in plan["runs"]:
            run = source.get("runId")
            require(run in RUNS and run not in hashes and
                    len(source.get("images", [])) == 18 and
                    len(source.get("immediatePriorOriginalFrames", [])) == 18,
                    "Unknown run or missing exact original temporal pairs.")
            path = f"annoteren/{run}/index.html"
            expected.add(path)
            html = z.read(path).decode("utf-8")
            require(html.count("<script>") == html.count("</script>") == 1 and
                    '"argus-m227-player-point-"' in html and
                    "localStorage.setItem(storageKey" in html and
                    "fetch(" not in html and '"state":"visible"' not in html and
                    "run-01" not in html and
                    "study.images.length!==18" in html,
                    "Unapproved or nonblank mobile reviewer.")
            encoded = re.search(r"const study=(\{.*?\});\s*const W=", html, re.S)
            require(encoded is not None and json.loads(encoded.group(1)) ==
                    {"schemaVersion": 1, "seed": SOURCE_SEED, "sourceSeed": SOURCE_SEED,
                     "runId": run, "partition": "development", "images": source["images"]},
                    "Public mobile original study image list differs from source.")
            published[path] = z.read(path)
            targets = {}
            for target, previous in zip(source["images"], source["immediatePriorOriginalFrames"]):
                step, name, hashed = target["step"], target["file"], target["sha256"]
                require(type(step) is int and step >= 1 and step <= 180 and
                        name == f"frame-{step:04d}.png" and
                        re.fullmatch("[a-f0-9]{64}", hashed) is not None,
                        "Unexpected original screenshot step or digest.")
                whole = f"annoteren/{run}/{name}"
                expected.add(whole)
                png = z.read(whole)
                require(24 <= len(png) <= 2_000_000 and png.startswith(PNG) and
                        struct.unpack(">II", png[16:24]) == (390, 844) and
                        digest(png) == hashed,
                        "Changed approved public target source PNG.")
                published[whole] = png
                targets[name] = hashed
                # Validate the 54 original context images, but DO NOT PUBLISH them.
                context = f"context/{run}/prev-for-{step:04d}.png"
                expected.add(context)
                old = z.read(context)
                require(previous.get("targetStep") == step and
                        previous.get("priorStep") == step-1 and
                        previous.get("file") == context and
                        len(old) >= 24 and old.startswith(PNG) and
                        struct.unpack(">II", old[16:24]) == (390, 844) and
                        digest(old) == previous.get("sha256"),
                        "Invalid/or modified private original temporal context.")
            require(len(targets) == 18, "Duplicate sampled screenshot step.")
            hashes[run] = targets
        require(set(hashes) == set(RUNS) and set(expected) == set(names) and
                len(published) == 57, "Unapproved extra file or prospective/owner material.")
    return published, hashes


def all_files(directory):
    return {p.relative_to(directory).as_posix(): p.read_bytes()
            for p in directory.rglob("*") if p.is_file() and not p.is_symlink()}


def tree_hash(files):
    return digest("".join(name + "\x00" + digest(raw) + "\n"
                          for name, raw in sorted(files.items())).encode())


def verify(site, previous=None, added=None, *, pinned=True):
    require(site.is_dir(), "Public site not found.")
    files = all_files(site)
    require(len(files) == 294 and files.get(".nojekyll") == b"" and
            len(list(site.glob("annoteren/*/frame-*.png"))) == 270 and
            len(list(site.glob("annoteren/*/index.html"))) == 21 and
            len(list(site.rglob("*.json"))) == 1 and
            not list(site.rglob("*.jsonl")) and
            not list(site.rglob("*.zip")) and
            not list(site.glob("context/**/*")),
            "Public-only allowlist count broken or private research source leaked.")
    manifest = json.loads(files["published-development-manifest.json"])
    require(manifest.get("m227SourceStudySeed") == SOURCE_SEED and
            manifest.get("m227PointRoutes") == [f"annoteren/{r}/" for r in RUNS] and
            manifest.get("m227HumanTruth") == "ALL_UNKNOWN" and
            set(manifest.get("m227Screenshots", {})) == set(RUNS),
            "Wrong published consented development manifest.")
    for run in RUNS:
        hashes = manifest["m227Screenshots"][run]
        require(len(hashes) == 18, "Unexpected original source frame count.")
        for filename, expected in hashes.items():
            path = f"annoteren/{run}/{filename}"
            require(re.fullmatch(r"frame-\d{4}\.png", filename) is not None and
                    digest(files.get(path, b"")) == expected,
                    "Published original development PNG SHA has changed.")
        html = files[f"annoteren/{run}/index.html"].decode("utf-8")
        require('"state":"visible"' not in html and "fetch(" not in html and
                "localStorage.setItem(storageKey" in html,
                "Human truth/upload was injected into the mobile reviewer.")
    require(all(f"annoteren/{run}/" not in name for run in ("dev-o", "dev-p", "dev-q")
                for name in files) and
            not any("run-01" in x for x in files),
            "Sealed prospective or original run-01 was exposed.")
    if previous is not None:
        old = all_files(previous)
        require(len(old) == 237, "Original public checkpoint has wrong count.")
        for path, raw in old.items():
            if path not in ("index.html", "published-development-manifest.json"):
                require(files.get(path) == raw, "Changed previously approved screenshot or HTML.")
        require(set(files) == set(old) | set(added or {}),
                "Unapproved new public file found.")
    if added is not None:
        require(all(files.get(path) == raw for path, raw in added.items()),
                "Original new screenshot or blank HTML bytes changed.")
    sha = tree_hash(files)
    if pinned:
        require(sha == SITE_TREE_SHA256,
                "Public site full-tree SHA differs from independently pinned checkpoint.")
    print("PASS M2.28: 294 exact files; 270 source PNGs, 21 blank reviewers; NO context/labels/prospectives.")
    return sha


def build(previous, archive, output):
    verify_previous_checkpoint(previous)
    require(not output.exists() or not any(output.iterdir()),
            "Do not modify an approved public checkpoint.")
    additions, hashes = read_original(archive)
    old = all_files(previous)
    require(len(old) == 237 and not any(f"annoteren/{r}/index.html" in old for r in RUNS),
            "Refuse to overwrite earlier public development study.")
    for name, data in {**old, **additions}.items():
        destination = output/name
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(data)
    path = output/"published-development-manifest.json"
    manifest = json.loads(path.read_text())
    require("m227Screenshots" not in manifest, "Refuse to replace existing original source study.")
    manifest.update({"m227SourceStudySeed": SOURCE_SEED,
                     "m227PointRoutes": [f"annoteren/{r}/" for r in RUNS],
                     "m227HumanTruth": "ALL_UNKNOWN", "m227Screenshots": hashes,
                     "m227ReviewConsent": "standing owner consent: original development PNGs and blank mobile HTML only"})
    path.write_text(json.dumps(manifest, sort_keys=True, indent=2) + "\n")
    homepage = output/"index.html"
    html = homepage.read_text()
    require(html.count("</ul>") == 1, "Cannot safely extend approved public reviewer homepage.")
    links = "".join(f'<li><a href="annoteren/{r}/">M2.27 {r}: 18 oorspronkelijke beelden beoordelen</a></li>' for r in RUNS)
    homepage.write_text(html.replace("</ul>", links+"</ul>", 1))
    print("NEW_M2_28_PUBLIC_SHA256=" + verify(output, previous, additions, pinned=False))


def main():
    p = argparse.ArgumentParser()
    sub = p.add_subparsers(dest="cmd", required=True)
    b = sub.add_parser("build")
    b.add_argument("--previous", required=True, type=Path)
    b.add_argument("--archive", required=True, type=Path)
    b.add_argument("--output", required=True, type=Path)
    v = sub.add_parser("verify")
    v.add_argument("--site", required=True, type=Path)
    a = p.parse_args()
    if a.cmd == "build":
        build(a.previous, a.archive, a.output)
    else:
        print("FULL_PUBLIC_SHA256="+verify(a.site))


if __name__ == "__main__":
    main()
