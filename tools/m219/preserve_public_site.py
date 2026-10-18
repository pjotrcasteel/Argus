#!/usr/bin/env python3
"""M2.19 — pin/recover the site ALREADY published with owner consent.

Exactly one already-published M2.18 Pages build archive is admitted by
archive SHA256 AND entry-by-entry content fingerprint. No research archive,
private owner labels, traces, model files or held-out screenshots are accepted.
"""
import argparse
import hashlib
import json
import re
import struct
from pathlib import Path
from zipfile import ZipFile

ARCHIVE_SHA256 = "deecd35447554711223f883ca8f636676aa136048176819c489931148304c416"
FILE_FINGERPRINT = "e3cb509b9394f30f7b652910103c882239e7ca64c8a2bb2c971445bcd58331e9"
RUNS = ("run-02", "dev-a", "dev-b", "dev-c", "dev-d", "dev-e", "dev-f", "dev-g", "dev-h")
POINT_RUNS = ("dev-f-points", "dev-g-points", "dev-h-points")
SHA = lambda b: hashlib.sha256(b).hexdigest()
PNG = b"\x89PNG\r\n\x1a\n"


def require(condition: bool, reason: str) -> None:
    if not condition:
        raise ValueError(reason)


def admitted_files(archive: Path):
    require(archive.is_file() and archive.stat().st_size < 25_000_000, "Missing/large approved site archive.")
    require(SHA(archive.read_bytes()) == ARCHIVE_SHA256, "NOT an approved public snapshot ZIP.")
    with ZipFile(archive) as zipped:
        files = zipped.namelist()
        expected = {"index.html", "published-development-manifest.json"}
        expected.update(f"annoteren/{run}/index.html" for run in (*RUNS, *POINT_RUNS))
        for run in RUNS:
            original_images = [name for name in files
                               if re.fullmatch(rf"annoteren/{re.escape(run)}/frame-\d{{4}}\.png", name)]
            require(len(original_images) == 12, f"Expected exactly 12 approved PNGs for {run}.")
            expected.update(original_images)
        require(len(files) == len(set(files)) == 122 and set(files) == expected,
                "Unexpected content: owner JSON/traces/unapproved screenshots are forbidden.")
        fingerprint = SHA("".join(
            name + "\x00" + SHA(zipped.read(name)) + "\n" for name in sorted(files)).encode())
        require(fingerprint == FILE_FINGERPRINT, "Published source assets were changed.")
        require(zipped.testzip() is None, "Corrupt published screenshot archive.")
        manifest = json.loads(zipped.read("published-development-manifest.json"))
        require(manifest.get("schemaVersion") == 1 and
                manifest.get("routes") == [f"annoteren/{run}/" for run in RUNS] and
                manifest.get("m218PointRoutes") == [f"annoteren/{run}/" for run in POINT_RUNS] and
                manifest.get("m215StudySeed") == "argus-m215-independent-spatial-probes-20261018-v1" and
                manifest.get("m218SourceStudySeed") == manifest["m215StudySeed"],
                "Public-only approved review routes differ.")
        for run in RUNS:
            for name in files:
                if not name.startswith(f"annoteren/{run}/frame-"):
                    continue
                png = zipped.read(name)
                require(24 <= len(png) < 2_000_000 and png[:8] == PNG and
                        struct.unpack(">II", png[16:24]) == (390, 844),
                        "Invalid public approved original screenshot.")
                if run != "run-02":
                    require(manifest["screenshots"][run][name.rsplit("/", 1)[1]] == SHA(png),
                            "Original owner-approved frame manifest differs.")
        for run in POINT_RUNS:
            name = f"annoteren/{run}/index.html"
            html = zipped.read(name).decode("utf-8")
            require("const study=" in html and "localStorage.setItem(storageKey" in html and
                    "fetch(" not in html and "run-01" not in html and
                    "\"state\":\"visible\"" not in html,
                    "Public center-review HTML contains suspicious ground-truth/prediction data.")
        return {name: zipped.read(name) for name in files}


def write_site(archive: Path, target: Path) -> None:
    files = admitted_files(archive)
    require(not target.exists() or not any(target.iterdir()),
            "Destination must be absent/empty to avoid unexpected retained files.")
    target.mkdir(parents=True, exist_ok=True)
    for name, raw in files.items():
        dest = target / name
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(raw)
    (target / ".nojekyll").write_bytes(b"")
    print(f"PASS M2.19: {len(files)} fixed public files, 108 authentic PNGs, 12 review routes. No private labels.")


def check_site(target: Path) -> None:
    require(target.is_dir(), "Pinned public site folder missing.")
    names = {p.relative_to(target).as_posix(): SHA(p.read_bytes())
             for p in target.rglob("*") if p.is_file() and not p.is_symlink()}
    require(".nojekyll" in names and names.pop(".nojekyll") == SHA(b""),
            "Missing/altered GitHub Pages root marker.")
    require(len(names) == 122, "Expected only 122 previously approved public files.")
    fingerprint = SHA("".join(
        name + "\x00" + digest + "\n" for name, digest in sorted(names.items())).encode())
    require(fingerprint == FILE_FINGERPRINT, "Persistent snapshot bytes differ from original Pages artifact.")
    print("PASS M2.19: persistent public repository snapshot exactly equals original 122-file Pages build.")


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="mode", required=True)
    create = sub.add_parser("extract")
    create.add_argument("--archive", type=Path, required=True)
    create.add_argument("--destination", type=Path, required=True)
    verify = sub.add_parser("verify")
    verify.add_argument("--site", type=Path, required=True)
    a = parser.parse_args()
    if a.mode == "extract":
        write_site(a.archive, a.destination)
    else:
        check_site(a.site)


if __name__ == "__main__":
    main()
