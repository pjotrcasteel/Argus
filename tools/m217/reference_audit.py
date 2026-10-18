#!/usr/bin/env python3
"""M2.17: local-only audit/review of ORIGINAL M2.15 candidate-location references.

Never uploads owner labels, mutates exports, repairs labels automatically, or
reads the old held-out run-01. Review HTML embeds ONLY contradictory original
development screenshots and original human exports, exclusively in --output.
"""
import argparse
import base64
import hashlib
import html
import json
import re
import struct
from pathlib import Path
from zipfile import ZipFile

SOURCE_SHA256 = "02599b8eae1d510225a4c94b209c0291978856497f6617e34e9ffd5cf90a4f6d"
SOURCE_SEED = "argus-m214-blind-24-bins-development-only-20261018"
STUDY_SEED = "argus-m215-independent-spatial-probes-20261018-v1"
RUNS = ("dev-f", "dev-g", "dev-h")
FIELDS = {"sampleId", "step", "x", "y", "radius", "role", "label"}
LABELS = {"present", "absent", "unknown"}


def sha256(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def inspect(archive_path: Path, label_paths: list[Path], expected_sha256: str = SOURCE_SHA256):
    """Return private audit plus the authenticated originals for a local reviewer.

    expected_sha256 may be blank ONLY in synthetic unit tests, never in the CLI.
    Even test fixtures must pass full frozen metadata, source PNG and label checks.
    """
    require(len(label_paths) == 3, "Exactly three owner exports required.")
    original_bytes = archive_path.read_bytes()
    if expected_sha256:
        require(sha256(original_bytes) == expected_sha256, "Unrecognized ORIGINAL M2.15 review ZIP SHA256.")
    owner = {}
    for path in label_paths:
        require(path.stat().st_size <= 100_000, "Oversized private human export.")
        value = json.loads(path.read_text(encoding="utf-8"))
        require(set(value) == {"schemaVersion", "studySeed", "runId", "probes"} and
                value["schemaVersion"] == 1 and value["studySeed"] == STUDY_SEED and
                value["runId"] in RUNS and value["runId"] not in owner, "Wrong or duplicate owner export.")
        owner[value["runId"]] = value
    require(set(owner) == set(RUNS), "All three development labels required.")

    issues = []
    counts = {}
    image_by_key = {}
    with ZipFile(archive_path) as zip_file:
        names = zip_file.namelist()
        require(len(names) == len(set(names)) == 41 and
                all(".." not in n and not n.startswith("/") for n in names),
                "Unexpected original archive contents.")
        plan = json.loads(zip_file.read("spatial-study-plan.json"))
        require(plan.get("schemaVersion") == 1 and plan.get("mode") == "development-only" and
                plan.get("sourceSeed") == SOURCE_SEED and plan.get("studySeed") == STUDY_SEED and
                plan.get("radiusPixels") == 26 and plan.get("humanTruth") == "ALL_UNKNOWN" and
                plan.get("runIds") == list(RUNS), "Unexpected or non-development frozen manifest.")
        groups = plan.get("runs", [])
        require(len(groups) == 3 and [g["runId"] for g in groups] == list(RUNS),
                "Different frozen development run ordering.")
        traces = set()
        allowed = {"spatial-study-plan.json", "index.html"}
        for group in groups:
            run = group["runId"]
            require(group.get("schemaVersion") == 1 and group.get("partition") == "development" and
                    re.fullmatch(r"[a-f0-9]{64}", group.get("traceSha256", "")) is not None,
                    "Invalid independent development provenance.")
            traces.add(group["traceSha256"])
            expected = group.get("candidates", [])
            images = group.get("images", [])
            require(group.get("candidateCount") == 24 and len(expected) == 24 and len(images) == 12,
                    "Frozen 12-image/24-probe protocol violated.")
            allowed.add(f"annoteren/{run}/index.html")
            image_steps = set()
            for image in images:
                step, filename = image["step"], image["file"]
                require(type(step) is int and step >= 0 and filename == f"frame-{step:04d}.png" and
                        step not in image_steps, "Invalid source image step.")
                image_steps.add(step)
                entry = f"annoteren/{run}/{filename}"
                allowed.add(entry)
                png = zip_file.read(entry)
                require(len(png) < 2_000_000 and png[:8] == b"\x89PNG\r\n\x1a\n" and
                        struct.unpack(">II", png[16:24]) == (390, 844) and
                        sha256(png) == image["sha256"], "Source PNG bytes/viewport/hash changed.")
                image_by_key[(run, step)] = png

            originals = {p["sampleId"]: p for p in expected}
            require(len(originals) == 24, "Duplicate frozen candidate.")
            actual = owner[run]["probes"]
            require(isinstance(actual, list) and len(actual) == 24, "Missing owner probes.")
            seen = set()
            by_step = {}
            counts[run] = {"present": 0, "absent": 0, "unknown": 0}
            for probe in actual:
                require(isinstance(probe, dict) and set(probe) == FIELDS, "Unexpected private label fields.")
                sample = probe["sampleId"]
                require(sample in originals and sample not in seen, "Unknown or repeated owner sample.")
                seen.add(sample)
                source = originals[sample]
                require(all(type(probe[k]) is type(source[k]) and probe[k] == source[k]
                            for k in ("sampleId", "step", "x", "y", "radius", "role")) and
                        source["label"] == "unknown" and probe["label"] in LABELS,
                        "Owner label changed a frozen location, role or radius.")
                require(probe["radius"] == 26 and probe["role"] in ("center-anchor", "spatial-control") and
                        probe["step"] in image_steps and 26 <= probe["x"] <= 364 and
                        26 <= probe["y"] <= 818, "Illegal original candidate.")
                if probe["role"] == "center-anchor":
                    require((probe["x"], probe["y"]) == (195, 422), "Frozen center moved.")
                by_step.setdefault(probe["step"], []).append(probe)
                counts[run][probe["label"]] += 1
            require(len(by_step) == 12 and all(
                {p["role"] for p in pair} == {"center-anchor", "spatial-control"} and len(pair) == 2
                for pair in by_step.values()), "Original paired probes changed.")
            for step, pair in sorted(by_step.items()):
                a, b = pair
                distance_squared = (a["x"] - b["x"]) ** 2 + (a["y"] - b["y"]) ** 2
                if a["label"] == b["label"] == "present" and distance_squared > (a["radius"] + b["radius"]) ** 2:
                    issues.append({"runId": run, "step": step, "reason": "DISJOINT_DOUBLE_PRESENT",
                                   "probes": [dict(a), dict(b)]})
        require(len(traces) == 3 and set(names) == allowed, "Unrecognized source archive files or trace overlap.")

    audit = {"schemaVersion": 1, "status": "REQUIRES_HUMAN_REVIEW" if issues else "REFERENCE_CONSISTENT",
             "studySeed": STUDY_SEED, "sourceArchiveSha256": sha256(original_bytes),
             "ownerExportSha256": {run: sha256(path.read_bytes()) for path in label_paths
                                   for run in [json.loads(path.read_text(encoding="utf-8"))["runId"]]},
             "counts": counts, "issues": issues,
             "note": "Integrity/contradiction audit only. No label edits, fitted weights or model accuracy."}
    return audit, owner, image_by_key


def build_reviewer(audit: dict, owner: dict, png_by_key: dict) -> str:
    """Self-contained local HTML containing private inputs; never publish its contents."""
    sections = []
    for index, item in enumerate(audit["issues"]):
        png = base64.b64encode(png_by_key[(item["runId"], item["step"])]).decode("ascii")
        cards = []
        for probe in item["probes"]:
            options = "".join(
                f'<option value="{state}"{" selected" if probe["label"] == state else ""}>{text}</option>'
                for state, text in (("present", "Ja, speler hier"), ("absent", "Nee, geen speler"), ("unknown", "Onbekend")))
            circle = (f'<svg class="overlay" viewBox="0 0 390 844" preserveAspectRatio="xMidYMid meet">'
                      f'<circle cx="{probe["x"]}" cy="{probe["y"]}" r="26" fill="none" stroke="#ffe16d" '
                      f'stroke-width="4"/></svg>')
            cards.append(f'<div class="card"><div class="screen">'
                         f'<img alt="Original screenshot" src="data:image/png;base64,{png}">{circle}</div>'
                         f'<div class="label">{html.escape(probe["role"])}: {html.escape(probe["sampleId"])}</div>'
                         f'<select data-run="{item["runId"]}" data-sample="{probe["sampleId"]}">{options}</select></div>')
        sections.append(f'<section><h2>{item["runId"]} · frame {item["step"]}</h2>'
                        f'<p>De twee locaties zijn te ver uit elkaar om allebei dezelfde speler te bevatten. '
                        f'Bekijk elke gele cirkel afzonderlijk. Kies alleen wat je werkelijk ziet.</p>'
                        f'<div class="cards">{"".join(cards)}</div>'
                        f'<p id="warning-{index}" class="warning"></p></section>')

    payload = json.dumps(owner, separators=(",", ":"), ensure_ascii=False).replace("<", "\\u003c")
    return f"""<!doctype html>
<html lang="nl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>ARGUS M2.17 — privé herbeoordeling</title>
<style>
:root{{color-scheme:dark;font-family:system-ui}}body{{margin:0;background:#0e1521;color:#f5f8ff}}
main{{max-width:1040px;margin:auto;padding:16px}}p{{line-height:1.45;color:#c0d2e7}}
section{{border-top:1px solid #46536e;margin:20px 0;padding:8px 0}}
.cards{{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px}}
.card{{min-width:0}}.screen{{width:100%;max-width:390px;aspect-ratio:390/844;position:relative;overflow:hidden}}
.screen img,.overlay{{position:absolute;inset:0;width:100%;height:100%;display:block}}
.label{{font-size:12px;overflow-wrap:anywhere;margin:8px 0}}
select,button{{font:inherit;width:100%;min-height:46px;border:1px solid #839ac7;border-radius:8px;
background:#25364e;color:#fff;padding:7px}}button:disabled{{opacity:.45}}
.warning{{color:#ffd36b}}.hint{{border-left:3px solid #eab85a;padding-left:10px}}
@media(max-width:460px){{.cards{{gap:8px}}.label{{font-size:11px}}}}
</style></head><body><main>
<h1>ARGUS — uitsluitend privé controleren</h1>
<p class="hint">Deze pagina bevat je oorspronkelijke beoordelingen en authentieke ontwikkelscreenshots.
Er worden geen gegevens verzonden. De oorspronkelijke JSON-bestanden worden nooit gewijzigd.
Na je keuze krijg je een <strong>nieuw</strong> gecorrigeerd JSON-bestand.</p>
{"".join(sections)}
<p class="warning" id="status"></p><button id="export" type="button">Download nieuw beoordeeld JSON-bestand</button>
<p>Dit is geen modeltraining. Laat een locatie op Onbekend staan als je niet zeker bent.</p>
</main><script>
"use strict";
const original={payload};
const issues={json.dumps(audit["issues"],separators=(",", ":"))};
const selects=[...document.querySelectorAll("select[data-sample]")];
function update(){{
 const conflicts=issues.filter(issue=>issue.probes.every(probe=>{{
  const node=selects.find(s=>s.dataset.sample===probe.sampleId);
  return node && node.value==="present";
 }}));
 document.getElementById("status").textContent=conflicts.length
   ? "Nog "+conflicts.length+" tegenstrijdige frame(s); export wordt geblokkeerd."
   : "Tegenstrijdigheid opgelost; nieuwe menselijke beoordeling kan privé worden geëxporteerd.";
 document.getElementById("export").disabled=conflicts.length>0;
}}
selects.forEach(node=>node.addEventListener("change",update));
document.getElementById("export").addEventListener("click",()=>{{
 const runs=[...new Set(issues.map(i=>i.runId))];
 for(const run of runs){{
  const result=JSON.parse(JSON.stringify(original[run]));
  for(const probe of result.probes){{
   const node=selects.find(s=>s.dataset.run===run && s.dataset.sample===probe.sampleId);
   if(node)probe.label=node.value;
  }}
  const url=URL.createObjectURL(new Blob([JSON.stringify(result,null,2)],{{type:"application/json"}}));
  const link=document.createElement("a");link.href=url;
  link.download="m215-"+run+"-labels-rechecked.json";
  document.body.appendChild(link);link.click();link.remove();
  setTimeout(()=>URL.revokeObjectURL(url),2000);
 }}
}});
update();
</script></body></html>
"""


def main() -> int:
    parser = argparse.ArgumentParser(description="Private original-image and frozen-probe consistency audit.")
    parser.add_argument("--archive", type=Path, required=True)
    for run in RUNS:
        parser.add_argument("--labels-" + run[-1], type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        report, original, images = inspect(args.archive, [args.labels_f, args.labels_g, args.labels_h])
        args.output.mkdir(parents=True, exist_ok=True)
        (args.output / "private-reference-audit.json").write_text(
            json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        if report["issues"]:
            (args.output / "private-recheck.html").write_text(
                build_reviewer(report, original, images), encoding="utf-8")
        print(f"M2.17: {report['status']}; {len(report['issues'])} conflict(s), local output only.")
        return 0
    except (OSError, ValueError, KeyError, TypeError, json.JSONDecodeError) as exc:
        print("M2.17 private audit rejected input: " + str(exc))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
