#!/usr/bin/env python3
"""Append owner-approved M2.15 ORIGINAL dev-FGH location review to existing ARGUS Pages.

Does NOT publish trace, training sets, privately completed human labels or held-out data.
Never rebuilds/replaces any of the 72 previously approved screenshot bytes or six HTML pages.
"""
import argparse
import hashlib
import json
import re
import struct
from pathlib import Path
from zipfile import ZipFile

RUNS = ("dev-f", "dev-g", "dev-h")
PREVIOUS = ("run-02", "dev-a", "dev-b", "dev-c", "dev-d", "dev-e")
STUDY_SEED = "argus-m215-independent-spatial-probes-20261018-v1"
PNG_MAGIC = b"\x89PNG\r\n\x1a\n"
MAX_IMAGE_BYTES = 2_000_000
# Hash of the ORIGINAL blind M2.15 HTML template after replacing ONLY its frozen
# inline study JSON with the sentinel. Pin this to the authentic PR45 archive:
# do not regenerate review pages from arbitrary external HTML.
ORIGINAL_TEMPLATE_SHA256 = "7d291b21118b9756ca70e86760b8fe9447119bdd68e1efb4cc146bdd22f167a4"
NEW_TEMPLATE = Path(__file__).resolve().parents[1] / "candidate-annotator" / "index.template.html"


def corrected_overlay_html(original_html: str, frozen_study_json: str) -> bytes:
    """Change presentation only, never M2.15 reference data or private label state.

    Rebuild from the vetted source template ONLY after proving the old HTML is
    byte-for-byte the authentic template with the original frozen study substituted.
    The new SVG uses pixel coordinates and a uniform viewBox transformation.
    """
    old_template = original_html.replace(frozen_study_json, "/*ARGUS_STUDY*/", 1)
    if checksum(old_template.encode("utf-8")) != ORIGINAL_TEMPLATE_SHA256:
        raise ValueError("Unrecognized original review page; refuse to rewrite its annotation behavior.")
    current = NEW_TEMPLATE.read_text(encoding="utf-8")
    if current.count("/*ARGUS_STUDY*/") != 1 or "preserveAspectRatio=\"xMidYMid meet\"" not in current:
        raise ValueError("Missing uniform source-image coordinate overlay template.")
    html = current.replace("/*ARGUS_STUDY*/", frozen_study_json, 1)
    if (html.count("<script>") != 1 or html.count("</script>") != 1
            or "localStorage.setItem(key" not in html or "playerX" in html or "threatLocations" in html
            or "run-01" in html):
        raise ValueError("Updated page must remain blind with local-only human references.")
    return html.encode("utf-8")


def checksum(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()


def preserve_previous(destination: Path) -> dict[str, str]:
    result = {}
    for run in PREVIOUS:
        folder = destination / "annoteren" / run
        entries = [folder / "index.html", *sorted(folder.glob("frame-*.png"))]
        if len(entries) != 13 or any(not p.is_file() or p.is_symlink() for p in entries):
            raise ValueError("All 72 previously approved screenshots and six HTML routes must be present.")
        result.update({p.relative_to(destination).as_posix(): checksum(p.read_bytes()) for p in entries})
    return result


def publish(archive: Path, destination: Path) -> None:
    originals = preserve_previous(destination)
    old_names = [f"annoteren/{name}/" for name in PREVIOUS]
    path = destination / "published-development-manifest.json"
    manifest = json.loads(path.read_text(encoding="utf-8"))
    if (manifest.get("schemaVersion") != 1 or manifest.get("routes") != old_names
            or set(manifest.get("screenshots", {})) != set(PREVIOUS[1:])
            or manifest.get("m27StudySeed") != "argus-m27-independent-development-frozen-20261017"):
        raise ValueError("Original six approved routes/provenance were modified.")

    with ZipFile(archive) as z:
        names = {entry.filename for entry in z.infolist() if not entry.is_dir()}
        if "spatial-study-plan.json" not in names:
            raise ValueError("Missing preregistered authentic spatial study.")
        plan = json.loads(z.read("spatial-study-plan.json"))
        groups = plan.get("runs")
        if (plan.get("schemaVersion") != 1 or plan.get("mode") != "development-only"
                or plan.get("studySeed") != STUDY_SEED
                or plan.get("sourceSeed") != "argus-m214-blind-24-bins-development-only-20261018"
                or plan.get("humanTruth") != "ALL_UNKNOWN" or plan.get("radiusPixels") != 26
                or plan.get("runIds") != list(RUNS) or not isinstance(groups, list)
                or [row.get("runId") for row in groups] != list(RUNS)):
            raise ValueError("Only original, precommitted, unlabeled M2.15 DEVELOPMENT images may publish.")
        hashes = set()
        allowed = {"spatial-study-plan.json", "index.html", ".nojekyll"}
        pending = {}
        for run, group in zip(RUNS, groups):
            if group.get("partition") != "development" or group.get("schemaVersion") != 1:
                raise ValueError("Not a development run.")
            trace_hash = group.get("traceSha256")
            if not isinstance(trace_hash, str) or not re.fullmatch(r"[a-f0-9]{64}", trace_hash):
                raise ValueError("Invalid independent trace provenance.")
            hashes.add(trace_hash)
            images = group.get("images")
            probes = group.get("candidates")
            if not isinstance(images, list) or len(images) != 12 or not isinstance(probes, list) or (
                len(probes) != 24 or group.get("candidateCount") != 24
            ):
                raise ValueError("Each review must show 12 originals and exactly 24 fixed locations.")
            steps = [img.get("step") for img in images]
            if steps != sorted(set(steps)) or any(type(step) is not int or step < 0 for step in steps):
                raise ValueError("Non-frozen/duplicate frame steps.")

            role_ids = set()
            count_by_step = {step: set() for step in steps}
            for probe in probes:
                step = probe.get("step")
                role = probe.get("role")
                key = probe.get("sampleId")
                if (step not in count_by_step or role not in ("center-anchor", "spatial-control")
                        or type(key) is not str or key != f"{run}-step{step:04d}-{role}"
                        or key in role_ids or probe.get("radius") != 26
                        or type(probe.get("x")) is not int or type(probe.get("y")) is not int
                        or not 26 <= probe["x"] <= 364 or not 26 <= probe["y"] <= 818
                        or probe.get("label") != "unknown"):
                    raise ValueError("A source review contains unapproved locations or human truth.")
                if role == "center-anchor" and (probe["x"], probe["y"]) != (195, 422):
                    raise ValueError("The registered central probe was moved.")
                role_ids.add(key)
                count_by_step[step].add(role)
            if any(roles != {"center-anchor", "spatial-control"} for roles in count_by_step.values()):
                raise ValueError("Missing a pair of independent local reference positions.")

            prefix = f"annoteren/{run}/"
            entry = prefix + "index.html"
            allowed.add(entry)
            html = z.read(entry).decode("utf-8")
            if (html.count("<script>") != 1 or html.count("</script>") != 1
                    or "/*ARGUS_STUDY*/" in html or "playerX" in html
                    or "threatLocations" in html or "run-01" in html):
                raise ValueError("Published reference HTML contains forbidden internal evidence.")
            matched = re.search(r"const study=(\{[^\n]+\});", html)
            if not matched:
                raise ValueError("Original mobile reviewer is missing its frozen study.")
            embedded = json.loads(matched.group(1))
            if embedded != {"seed": STUDY_SEED, **group}:
                raise ValueError("Original HTML changed candidate coordinates or human truth.")
            if "localStorage.setItem(key" not in html or 'link.download="m215-"+study.runId+"-labels.json"' not in html:
                raise ValueError("Reviewer has lost local-only save and export behavior.")
            # Preserve original source data, image bytes and user-label keys.
            # Only the independently vetted template changes how the highlighted circle renders.
            pending[entry] = corrected_overlay_html(html, matched.group(1))
            digest = {}
            for img in images:
                step = img["step"]
                filename = f"frame-{step:04d}.png"
                name = prefix + filename
                allowed.add(name)
                if img.get("file") != filename or not re.fullmatch(r"[a-f0-9]{64}", img.get("sha256", "")):
                    raise ValueError("Unexpected original image metadata.")
                raw = z.read(name)
                if (len(raw) < 24 or len(raw) > MAX_IMAGE_BYTES or not raw.startswith(PNG_MAGIC)
                        or struct.unpack(">II", raw[16:24]) != (390, 844)
                        or checksum(raw) != img["sha256"]):
                    raise ValueError("Original recorded screenshot bytes do not match authentic frozen hash.")
                pending[name] = raw
                digest[filename] = checksum(raw)
            if (destination / prefix).exists():
                raise ValueError("Refuse to overwrite any previously hosted human reference.")
            manifest["screenshots"][run] = digest
            manifest["routes"].append(prefix)

        if len(hashes) != 3:
            raise ValueError("At least one original development screenshot trace was repeated.")
        if names - allowed:
            raise ValueError("Archive unexpectedly includes private labels, trace, extra screenshots or metadata: "
                             + ", ".join(sorted(names - allowed)[:5]))
        for name, contents in pending.items():
            target = destination / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(contents)

    if preserve_previous(destination) != originals:
        raise ValueError("Previously hosted review pages/images changed during publication.")
    manifest["m215StudySeed"] = STUDY_SEED
    manifest["m215ReviewConsent"] = "Owner explicitly granted standing consent for ARGUS DEVELOPMENT review screenshots, 2026-10-18; never human labels/held-out."
    manifest["m215CandidatesPerRun"] = 24
    path.write_text(json.dumps(manifest, sort_keys=True, indent=2) + "\n", encoding="utf-8")
    homepage = destination / "index.html"
    prior = homepage.read_text(encoding="utf-8")
    if prior.count("</ul>") != 1:
        raise ValueError("Old homepage missing unique review list.")
    new_links = "".join(
        f'<li><a href="annoteren/{run}/">M2.15 {run.upper()}: 24 plaatsen beoordelen op 12 beelden</a></li>'
        for run in RUNS)
    homepage.write_text(prior.replace("</ul>", new_links + "</ul>", 1), encoding="utf-8")
    print("Published only explicitly approved 36 original PNGs, 72 UNKNOWN location probes; 6 earlier pages unchanged.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--destination", type=Path, required=True)
    args = parser.parse_args()
    publish(args.archive, args.destination)
