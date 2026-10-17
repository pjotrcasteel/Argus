#!/usr/bin/env python3
"""Append 36 explicitly owner-approved M2.7 *development* images to the existing ARGUS Pages site.

Original M2.4 and M2.6b review pages are rebuilt first with their unchanged code.
This tool never copies traces, private annotations, candidate guesses or held-out images
to the published site. All 36 image bytes are checked against the frozen M2.7 study.
"""
import argparse
import hashlib
import json
import re
import struct
from pathlib import Path
from zipfile import ZipFile

RUNS = ("dev-c", "dev-d", "dev-e")
SEED = "argus-m27-independent-development-frozen-20261017"
PNG_MAGIC = b"\x89PNG\r\n\x1a\n"
MAX_PNG_SIZE = 2_000_000
OLD_ROUTES = ["annoteren/run-02/", "annoteren/dev-a/", "annoteren/dev-b/"]


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def published_assets(destination: Path) -> dict[str, str]:
    """Hash all previous review HTML/PNG files: previous original data is immutable."""
    originals = {}
    for run in ("run-02", "dev-a", "dev-b"):
        folder = destination / "annoteren" / run
        files = [folder / "index.html", *sorted(folder.glob("frame-*.png"))]
        if len(files) != 13 or not all(file.is_file() and not file.is_symlink() for file in files):
            raise ValueError("All 12 previously published images and their HTML must remain present.")
        originals.update({str(file.relative_to(destination)): sha(file.read_bytes()) for file in files})
    return originals


def publish(mobile_archive: Path, research_archive: Path, destination: Path) -> None:
    originals = published_assets(destination)
    manifest_path = destination / "published-development-manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest.get("schemaVersion") != 1 or manifest.get("routes") != OLD_ROUTES or set(
        manifest.get("screenshots", {})
    ) != {"dev-a", "dev-b"} or len(manifest.get("preservedRun02HtmlSha256", "")) != 64:
        raise ValueError("Existing review route provenance must be validated before extension.")

    with ZipFile(research_archive) as research, ZipFile(mobile_archive) as mobile:
        frozen = json.loads(research.read("study-plan.json"))
        assignments = frozen.get("assignments")
        if frozen.get("schemaVersion") != 1 or frozen.get("mode") != "development-only" or (
            frozen.get("seed") != SEED or not isinstance(assignments, list)
        ) or sorted(a.get("runId") for a in assignments) != sorted(RUNS):
            raise ValueError("Unexpected independent development study or sample seed.")
        by_run = {a["runId"]: a for a in assignments}

        for run in RUNS:
            assignment = by_run[run]
            steps = assignment.get("frameSteps")
            if assignment.get("partition") != "development" or not isinstance(steps, list) or (
                len(steps) != 12 or steps != sorted(set(steps))
            ) or any(not isinstance(n, int) or isinstance(n, bool) or n < 0 for n in steps):
                raise ValueError("New public review must match 12 frozen development-only steps.")

            # Confirm the archived mobile page and its exported human schema are blank.
            prefix = f"annoteren/{run}/"
            document = mobile.read(prefix + "index.html").decode("utf-8")
            if document.count("<script>") != 1 or document.count("</script>") != 1 or any(
                token in document for token in ("/*ARGUS_FRAMES*/", "/*ARGUS_LABELS*/",
                                               "threatLocations", "playerX", "run-01", "run-02")
            ):
                raise ValueError("Unblinded or malformed archived mobile page for " + run)
            match_frames = re.search(r"const frames=(\[.*?\]);", document)
            match_labels = re.search(r"const original=(\{.*?\});", document)
            if match_frames is None or match_labels is None:
                raise ValueError("Could not independently verify original mobile image references.")
            frames = json.loads(match_frames.group(1))
            labels = json.loads(match_labels.group(1))
            source_labels = json.loads(research.read(run + "/blind-labels.template.json"))
            if labels != source_labels or labels.get("runId") != run or labels.get("schemaVersion") != 1 or (
                [x.get("step") for x in labels.get("frames", [])] != steps
            ) or [x.get("step") for x in frames] != steps:
                raise ValueError("Review HTML must match frozen blank label template and sample order.")
            if any(x.get("player") != {"state": "unknown", "center": None} or
                   x.get("threats") != {"state": "unknown", "centers": None}
                   for x in labels["frames"]):
                raise ValueError("Never publish a guessed or imported human annotation.")

            target = destination / prefix
            if target.exists():
                raise ValueError("New study route already exists; refuse overwrite.")
            target.mkdir(parents=True)
            (target / "index.html").write_text(document, encoding="utf-8")

            checksum = {}
            for frame in frames:
                step = frame["step"]
                name = f"frame-{step:04d}.png"
                if frame.get("file") != name or frame.get("width") != 390 or frame.get("height") != 844:
                    raise ValueError("Source screenshot metadata does not match expected viewport.")
                raw = mobile.read(prefix + name)
                original = research.read(run + "/" + name)
                if raw != original or len(raw) < 24 or len(raw) > MAX_PNG_SIZE or raw[:8] != PNG_MAGIC or (
                    struct.unpack(">II", raw[16:24]) != (390, 844)
                ):
                    raise ValueError("Published PNG differs from preregistered real screenshot.")
                (target / name).write_bytes(raw)
                checksum[name] = sha(raw)

            # The privacy boundary: screenshot pixels and blank page only.
            manifest["screenshots"][run] = checksum
            manifest["routes"].append(prefix)
            print(f"Published approved {run}: 12 checked original screenshots; no human guesses.")

    if originals != published_assets(destination):
        raise ValueError("One or more previously published original review files were changed.")
    if manifest["routes"] != OLD_ROUTES + [f"annoteren/{run}/" for run in RUNS]:
        raise ValueError("Unexpected public route ordering.")

    manifest["m27StudySeed"] = SEED
    manifest["m27ReviewConsent"] = "explicitly owner-approved 2026-10-17"
    manifest_path.write_text(json.dumps(manifest, sort_keys=True, indent=2), encoding="utf-8")

    # Link existing and newly approved locations without replacing saved local labels.
    homepage = destination / "index.html"
    html = homepage.read_text(encoding="utf-8")
    marker = '<ul><li><a href="annoteren/dev-a/">'
    if html.count(marker) != 1:
        raise ValueError("Existing website homepage has unexpected structure.")
    addition = "".join(
        f'<li><a href="annoteren/{run}/">Nieuwe development-run {run.upper()} — 12 screenshots</a></li>'
        for run in RUNS
    )
    html = html.replace("</ul>", addition + "</ul>", 1)
    homepage.write_text(html, encoding="utf-8")
    print("Existing 36 review PNGs untouched; 36 new PNGs appended; all 6 routes available.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--mobile-archive", type=Path, required=True)
    parser.add_argument("--research-archive", type=Path, required=True)
    parser.add_argument("--destination", type=Path, required=True)
    options = parser.parse_args()
    publish(options.mobile_archive, options.research_archive, options.destination)
