"""Offline integrity replay of M2.38 frozen authentic PNGs; never runs the game.

Checks genuine original-browser traces, deterministic blind selection,
byte-identical copied original/context PNGs, and no review-label leakage.
Does not infer a player position, game outcome or training accuracy.
"""
import argparse
import json
from pathlib import Path

from tools.m238 import fresh_capture as m


def validate(source_root: Path, review_root: Path) -> dict:
    m.require(source_root.is_dir() and review_root.is_dir(), "Missing frozen capture and review directories.")
    manifest_path = review_root / "study-manifest.json"
    m.require(manifest_path.is_file() and not manifest_path.is_symlink(), "Missing frozen study manifest.")
    manifest = json.loads(manifest_path.read_text())
    m.require(
        manifest.get("schemaVersion") == 1 and
        manifest.get("study") == "M2.38_NEW_EXTERNAL_DEVELOPMENT" and
        manifest.get("researchStatus") == "NOT_EVALUATED" and
        manifest.get("gameUrl") == m.URL and
        manifest.get("gameVersion") is None and
        manifest.get("versionAttested") is False and
        manifest.get("sourceSeed") == m.SEED and
        manifest.get("runIds") == list(m.RUNS) and
        manifest.get("sampledFramesPerRun") == m.TARGETS and
        manifest.get("originalTargetCount") == len(m.RUNS) * m.TARGETS and
        manifest.get("originalImmediatePredecessorCount") == len(m.RUNS) * m.TARGETS and
        manifest.get("ownerGroundTruth") == "NONE" and
        manifest.get("prospectiveSealedSourcesAccessed") is False and
        manifest.get("publicPublicationAuthorized") is False and
        manifest.get("selectionRule") == "SHA-seeded chronological time bins, never predictions/labels",
        "NOT_READY: frozen blind source protocol changed or leaked identity claims.")

    entries = manifest.get("runs")
    m.require(isinstance(entries, list) and
              [entry.get("runId") for entry in entries] == list(m.RUNS),
              "NOT_READY: missing or reordered independent source runs.")
    sources = [m.verify_run(source_root / run, run) for run in m.RUNS]
    m.require(len({entry["traceSha256"] for entry in sources}) == len(m.RUNS),
              "NOT_READY: original traces are not independent.")

    allowed = {"study-manifest.json", "index.html"}
    seen = set()
    details = []
    for run, record, source in zip(m.RUNS, entries, sources):
        folder = source_root / run
        m.require(record.get("sourceTraceSha256") == source["traceSha256"] and
                  record.get("sourceManifestSha256") == m.sha((folder / "run-manifest.json").read_bytes()) and
                  record.get("frames") == source["frames"] and
                  record.get("gameVersion") is None and
                  record.get("humanGroundTruth") == "ALL_UNKNOWN",
                  "NOT_READY: source manifest/trace differs from the frozen blind review.")
        images = record.get("images")
        contexts = record.get("immediatePriorOriginalFrames")
        m.require(isinstance(images, list) and isinstance(contexts, list) and
                  len(images) == len(contexts) == m.TARGETS,
                  "NOT_READY: blind targets or original preceding frames missing.")

        reviewer_file = f"annoteren/{run}/index.html"
        allowed.add(reviewer_file)
        html = (review_root / reviewer_file).read_text()
        m.require(run in html and "/^m238-[abc]$/" in html and
                  "fetch(" not in html and "playerX" not in html and
                  "threatLocations" not in html and
                  "verifiedPlayerCenter" not in html,
                  "NOT_READY: review UI may disclose predictions or network data.")

        steps = []
        for bin_no, (image, context) in enumerate(zip(images, contexts)):
            frame_count = source["frames"]
            low = max(1, bin_no * frame_count // m.TARGETS)
            high = (bin_no + 1) * frame_count // m.TARGETS
            m.require(high > low, "NOT_READY: empty sampling time bin.")
            signature = m.sha((m.SEED + "\n" + run + "\n" + str(bin_no)).encode())
            step = low + int(signature[:16], 16) % (high - low)
            steps.append(step)

            picture = f"frame-{step:04d}.png"
            predecessor = f"prev-for-{step:04d}.png"
            target_rel = f"annoteren/{run}/{picture}"
            context_rel = f"context/{run}/{predecessor}"
            m.require(image.get("step") == step and image.get("file") == picture and
                      context.get("targetStep") == step and
                      context.get("priorStep") == step - 1 and
                      context.get("file") == context_rel,
                      "NOT_READY: frozen image order or predecessor is not deterministic.")

            original = m.real_png(folder / picture)
            copied = m.real_png(review_root / target_rel)
            prior_original = m.real_png(folder / f"frame-{step - 1:04d}.png")
            prior_copy = m.real_png(review_root / context_rel)
            m.require(original == copied and m.sha(original) == image.get("sha256") and
                      prior_original == prior_copy and m.sha(prior_original) == context.get("sha256"),
                      "NOT_READY: original screenshot byte SHA or true predecessor mismatch.")
            allowed.update((target_rel, context_rel))
            seen.add(image["sha256"])

        m.require(steps == sorted(set(steps)), "NOT_READY: nonchronological or repeated targets.")
        details.append({"runId": run, "frames": source["frames"],
                        "traceSha256": source["traceSha256"], "reviewTargets": len(steps)})

    m.require(manifest.get("uniqueOriginalTargetDigests") == len(seen),
              "NOT_READY: frozen target uniqueness accounting is incorrect.")
    actual = set()
    for path in review_root.rglob("*"):
        m.require(not path.is_symlink(), "NOT_READY: frozen review contains a symlink.")
        if path.is_file():
            actual.add(path.relative_to(review_root).as_posix())
    m.require(actual == allowed, "NOT_READY: unexpected files, labels or missing originals in private review.")
    index = (review_root / "index.html").read_text()
    m.require(all(f"annoteren/{run}/" in index for run in m.RUNS) and
              "fetch(" not in index and "playerX" not in index,
              "NOT_READY: private review index is incomplete or not isolated.")
    return {"status": "SOURCE_VERIFIED_REVIEW_NOT_EVALUATED",
            "sourceRuns": details, "originalTargets": len(m.RUNS) * m.TARGETS,
            "uniqueTargetDigests": len(seen), "authenticPredecessors": len(m.RUNS) * m.TARGETS,
            "humanLabels": "NONE", "gameVersion": None, "publicPublicationAuthorized": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sources", required=True, type=Path)
    parser.add_argument("--review", required=True, type=Path)
    arguments = parser.parse_args()
    print(json.dumps(validate(arguments.sources, arguments.review), indent=2))


if __name__ == "__main__":
    main()
