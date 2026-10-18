#!/usr/bin/env python3
"""M2.18 — original-image player-point labels; zero model/prediction inputs.

Public BUILD accepts only previously owner-approved F/G/H ORIGINAL development
screenshots and adds blank image-click reviewer pages without uploading labels.
Private AUDIT validates new exported true player centers and derives independent
pixel locations for later evaluation. No held-out data, model or live policy.
"""
import argparse
import hashlib
import json
import re
import struct
from pathlib import Path
from zipfile import ZipFile

SOURCE_SHA256 = "02599b8eae1d510225a4c94b209c0291978856497f6617e34e9ffd5cf90a4f6d"
SOURCE_SEED = "argus-m215-independent-spatial-probes-20261018-v1"
POINT_SEED = "argus-m218-independent-player-center-reference-20261018-v1"
RUNS = ("dev-f", "dev-g", "dev-h")
VIEWPORT = (390, 844)
POSITIONS = ((72, 180), (195, 180), (318, 180),
             (72, 422), (195, 422), (318, 422),
             (72, 650), (195, 650), (318, 650))
TEMPLATE = Path(__file__).resolve().parents[2] / "web/point-annotator/index.template.html"


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def require(yes: bool, why: str) -> None:
    if not yes:
        raise ValueError(why)


def original_images(archive: Path, expected_sha: str = SOURCE_SHA256):
    """Return immutable development images and metadata. Override digest ONLY for artificial tests."""
    require(archive.stat().st_size < 10_000_000, "Source ZIP exceeds bounded review size.")
    if expected_sha:
        require(digest(archive.read_bytes()) == expected_sha, "Original frozen M2.15 ZIP SHA256 mismatch.")
    with ZipFile(archive) as z:
        names = z.namelist()
        require(len(names) == len(set(names)) == 41 and
                all(".." not in n and not n.startswith("/") for n in names),
                "Unexpected source ZIP entries.")
        plan = json.loads(z.read("spatial-study-plan.json"))
        require(plan.get("schemaVersion") == 1 and plan.get("mode") == "development-only" and
                plan.get("humanTruth") == "ALL_UNKNOWN" and plan.get("studySeed") == SOURCE_SEED and
                plan.get("runIds") == list(RUNS) and plan.get("radiusPixels") == 26,
                "Unexpected source development-only protocol.")
        runs = plan.get("runs", [])
        require(len(runs) == 3 and [r.get("runId") for r in runs] == list(RUNS),
                "Original independent run identities changed.")
        expected = {"spatial-study-plan.json", "index.html"}
        result = {}
        hashes = set()
        for run, item in zip(RUNS, runs):
            require(item.get("partition") == "development" and item.get("schemaVersion") == 1,
                    "Only original development source is allowed.")
            trace = item.get("traceSha256")
            require(isinstance(trace, str) and re.fullmatch("[a-f0-9]{64}", trace) and trace not in hashes,
                    "Independent original run trace SHA256 missing/repeated.")
            hashes.add(trace)
            images = item.get("images", [])
            candidates = item.get("candidates", [])
            require(len(images) == 12 and len(candidates) == 24 and
                    item.get("candidateCount") == 24 and
                    all(x.get("label") == "unknown" for x in candidates),
                    "Original frozen study must be wholly unannotated.")
            seen = set()
            expected.add(f"annoteren/{run}/index.html")
            listed = []
            for img in images:
                step, name = img["step"], img["file"]
                require(type(step) is int and step >= 0 and step not in seen and
                        name == f"frame-{step:04d}.png", "Invalid frozen original frame name/step.")
                seen.add(step)
                entry = f"annoteren/{run}/{name}"
                expected.add(entry)
                raw = z.read(entry)
                require(24 <= len(raw) < 2_000_000 and raw[:8] == b"\x89PNG\r\n\x1a\n" and
                        struct.unpack(">II", raw[16:24]) == VIEWPORT and digest(raw) == img["sha256"],
                        "Original screenshot bytes/viewport/hash changed.")
                listed.append({"step": step, "file": name, "sha256": img["sha256"]})
            require([i["step"] for i in listed] == sorted(seen),
                    "Source temporal order altered.")
            require(len({x["sampleId"] for x in candidates}) == 24 and
                    all(x["step"] in seen for x in candidates), "Frozen candidate list changed.")
            result[run] = listed
        require(set(names) == expected, "ZIP includes extra files (possible labels/held-out material).")
        return result


def public_build(archive: Path, site: Path, source_sha: str = SOURCE_SHA256):
    images = original_images(archive, source_sha)
    old_files = {p.relative_to(site).as_posix(): digest(p.read_bytes())
                 for p in site.rglob("*") if p.is_file()}
    require(all((site / "annoteren" / run / "index.html").is_file() for run in RUNS),
            "Original owner-approved public dev F/G/H pages must already exist.")
    for run in RUNS:
        for img in images[run]:
            file = site / "annoteren" / run / img["file"]
            require(file.is_file() and digest(file.read_bytes()) == img["sha256"],
                    "Published original screenshot differs from frozen source.")
    template = TEMPLATE.read_text(encoding="utf-8")
    require(template.count("/*ARGUS_POINT_STUDY*/") == 1 and
            "localStorage.setItem(storageKey" in template and "fetch(" not in template and
            "playerX" not in template and "threatLocations" not in template,
            "New reference template must remain blind and local-only.")
    manifest_path = site / "published-development-manifest.json"
    manifest = json.loads(manifest_path.read_text())
    require(manifest.get("m215StudySeed") == SOURCE_SEED and
            manifest.get("routes", [])[-3:] == [f"annoteren/{run}/" for run in RUNS] and
            "m218PointRoutes" not in manifest, "Existing nine-route provenance differs.")
    for run in RUNS:
        path = site / "annoteren" / (run + "-points") / "index.html"
        require(not path.exists(), "Refuse to overwrite previously published point review.")
        path.parent.mkdir(parents=True, exist_ok=False)
        data = {"schemaVersion": 1, "seed": POINT_SEED,
                "sourceSeed": SOURCE_SEED, "runId": run,
                "partition": "development", "images": images[run]}
        payload = json.dumps(data, separators=(",", ":")).replace("<", "\\u003c")
        path.write_text(template.replace("/*ARGUS_POINT_STUDY*/", payload), encoding="utf-8")
    manifest["m218PointRoutes"] = [f"annoteren/{run}-points/" for run in RUNS]
    manifest["m218SourceStudySeed"] = SOURCE_SEED
    manifest["m218Review"] = "Owner-approved original development PNGs only; all player center labels initially UNKNOWN."
    manifest_path.write_text(json.dumps(manifest, sort_keys=True, indent=2) + "\n")
    homepage = site / "index.html"
    home = homepage.read_text(encoding="utf-8")
    require(home.count("</ul>") == 1, "Existing homepage missing review list.")
    links = "".join(f'<li><a href="annoteren/{run}-points/">M2.18 {run}: tik de speler aan (12 beelden)</a></li>'
                    for run in RUNS)
    homepage.write_text(home.replace("</ul>", links + "</ul>", 1), encoding="utf-8")
    permitted = {"index.html", "published-development-manifest.json"}
    for name, original_digest in old_files.items():
        if name not in permitted:
            require(digest((site / name).read_bytes()) == original_digest,
                    "Previously approved public review page or screenshot changed.")
    require(len(list((site / "annoteren").glob("*-points/frame-*.png"))) == 0,
            "Never duplicate or change the 36 previously approved originals.")
    print("M2.18: three new BLIND point-location routes, 12 frames each; 108 original PNGs preserved.")


def validate_private(archive: Path, paths: list[Path], source_sha: str = SOURCE_SHA256) -> dict:
    images = original_images(archive, source_sha)
    require(len(paths) == 3, "Exactly three development owner export files expected.")
    seen = set()
    result = {}
    for path in paths:
        require(path.stat().st_size <= 100_000, "Private owner export too large.")
        data = json.loads(path.read_text(encoding="utf-8"))
        run = data.get("runId")
        require(run in RUNS and run not in seen and set(data) ==
                {"schemaVersion", "studySeed", "sourceStudySeed", "runId", "partition", "frames"} and
                data["schemaVersion"] == 1 and data["studySeed"] == POINT_SEED and
                data["sourceStudySeed"] == SOURCE_SEED and data["partition"] == "development",
                "Unrecognized/duplicate/held-out owner export.")
        seen.add(run)
        frames = data.get("frames")
        require(isinstance(frames, list) and len(frames) == 12, "Frame count differs from frozen plan.")
        count = {"visible": 0, "absent": 0, "unknown": 0}
        cells = []
        source = images[run]
        positives = []
        for frame, original in zip(frames, source):
            require(isinstance(frame, dict) and set(frame) ==
                    {"step", "screenshotSha256", "player"} and
                    frame["step"] == original["step"] and
                    frame["screenshotSha256"] == original["sha256"], "Source frame/order/hash changed.")
            player = frame["player"]
            require(isinstance(player, dict) and set(player) == {"state", "center"} and
                    player["state"] in count, "Invalid human point state.")
            state, point = player["state"], player["center"]
            if state == "visible":
                require(isinstance(point, dict) and set(point) == {"x", "y"} and
                        type(point["x"]) is int and type(point["y"]) is int and
                        0 <= point["x"] < VIEWPORT[0] and 0 <= point["y"] < VIEWPORT[1],
                        "Visible player requires an in-bounds integer center.")
                x, y = point["x"], point["y"]
                positives.append((x, y))
                cells.append({"runId": run, "step": frame["step"], "x": x, "y": y,
                              "label": "present", "source": "owner-click"})
                # One independently identified single player cannot simultaneously occupy
                # distant 26px image windows. These are *derived* negatives only.
                negatives = [(a, b) for a, b in POSITIONS if (a-x)**2+(b-y)**2 >= 70**2]
            else:
                require(point is None, "Absent/unknown player center must be null.")
                negatives = POSITIONS if state == "absent" else ()
            for x, y in negatives:
                cells.append({"runId": run, "step": frame["step"], "x": x, "y": y,
                              "label": "absent", "source": "single-player-spatial-exclusion"
                              if state == "visible" else "owner-confirmed-no-player"})
            count[state] += 1
        far = sum((x-195)**2+(y-422)**2 >= 70**2 for x, y in positives)
        status = "STUDY_NOT_READY"
        if count["visible"] >= 3 and count["absent"] >= 3 and far >= 2:
            status = "ELIGIBLE_FOR_FUTURE_MODEL_STUDY"
        result[run] = {"frames": count, "offCenterVisible": far,
                       "status": status, "derivedLocationCount": len(cells),
                       "derivedRows": cells}
    require(seen == set(RUNS), "Missing independent development run owner export.")
    return {"schemaVersion": 1, "studySeed": POINT_SEED,
            "status": ("STRUCTURALLY_READY" if all(v["status"] ==
                       "ELIGIBLE_FOR_FUTURE_MODEL_STUDY" for v in result.values())
                       else "STUDY_NOT_READY"),
            "runs": result,
            "warning": "Development-only correlated image patches, not independent game runs. "
                       "No ML model trained, no gameplay changes, original held-out untouched."}


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="command", required=True)
    build = sub.add_parser("build")
    build.add_argument("--archive", type=Path, required=True)
    build.add_argument("--site", type=Path, required=True)
    assess = sub.add_parser("audit")
    assess.add_argument("--archive", type=Path, required=True)
    for run in RUNS:
        assess.add_argument("--labels-" + run[-1], type=Path, required=True)
    assess.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.command == "build":
        public_build(args.archive, args.site)
    else:
        report = validate_private(args.archive, [args.labels_f, args.labels_g, args.labels_h])
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        print("M2.18 PRIVATE reference audit: " + report["status"] + "; no model fitted.")


if __name__ == "__main__":
    main()
