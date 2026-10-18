#!/usr/bin/env python3
"""M2.23 preregistered original-screenshot freeze for fresh L/M/N and sealed O/P/Q.

Never inspect predictions, game phase, player location, outcome or future labels
to select images. Prospective cohort's pixels are SEALED into an independent
Actions artifact and must not be opened or modeled before a locked final model.
"""
import argparse
import hashlib
import json
import re
import struct
import sys
from pathlib import Path

DEVELOPMENT = ("dev-l", "dev-m", "dev-n")
PROSPECTIVE = ("dev-o", "dev-p", "dev-q")
SEEDS = {"development": "argus-m223-fresh-development-20261018-v1",
         "prospective": "argus-m223-sealed-prospective-20261018-v1"}
SAMPLES = 18
PNG = b"\x89PNG\r\n\x1a\n"
sys.path.insert(0, str(Path(__file__).resolve().parents[2]))


def digest(data):
    return hashlib.sha256(data).hexdigest()


def require(ok, reason):
    if not ok:
        raise ValueError(reason)


def original_frames(root, role, destination):
    expected = DEVELOPMENT if role == "development" else PROSPECTIVE
    require(not destination.exists() or not any(destination.iterdir()),
            "Refuse to rewrite frozen study or prospective reference.")
    frozen = []
    trace_hashes = set()
    selected_digests = set()
    all_files = []
    for run in expected:
        source = root / run
        require(source.is_dir() and not source.is_symlink(), "Missing original game capture: "+run)
        manifest_bytes = (source / "run-manifest.json").read_bytes()
        manifest = json.loads(manifest_bytes)
        require(manifest.get("schemaVersion") == 1 and
                manifest.get("partition") == "development" and
                manifest.get("runId") == run and
                manifest.get("runnerExitCode") == 0 and
                manifest.get("actualGameOutcome") == "unknown" and
                manifest.get("viewport") == "390x844" and
                manifest.get("visualEvidenceGate") is True,
                "Source capture failed visual evidence or provenance gate.")
        raw_trace = (source / "trace.jsonl").read_bytes()
        sha = digest(raw_trace)
        require(sha == manifest.get("traceSha256") and sha not in trace_hashes,
                "Missing/changed/repeated source whole-game trace.")
        trace_hashes.add(sha)
        lines = [json.loads(line) for line in raw_trace.splitlines()]
        require(36 <= len(lines) <= 180 and manifest.get("frames") == len(lines),
                "Insufficient original observed game screenshots.")
        require(all(line.get("step") == i and line.get("screenshot") == f"frame-{i:04d}.png"
                    for i,line in enumerate(lines)),
                "Raw original frame ordering changed.")
        chosen = []
        records = []
        for bin_id in range(SAMPLES):
            lo,hi = bin_id*len(lines)//SAMPLES,(bin_id+1)*len(lines)//SAMPLES
            require(hi > lo, "Empty preregistered time bin.")
            h = hashlib.sha256(f"{SEEDS[role]}\n{run}\n{bin_id}".encode()).digest()
            step = lo+int.from_bytes(h[:8],"big")%(hi-lo)
            filename = f"frame-{step:04d}.png"
            file = source / filename
            require(file.is_file() and not file.is_symlink(), "Original frozen screenshot missing.")
            raw = file.read_bytes()
            require(24 <= len(raw) <= 2_000_000 and raw.startswith(PNG) and
                    struct.unpack(">II",raw[16:24]) == (390,844),
                    "Original screenshot PNG dimensions/pixels invalid.")
            sha_image = digest(raw)
            selected_digests.add(sha_image)
            records.append({"step":step,"file":filename,"sha256":sha_image})
            chosen.append(step)
            all_files.append((destination/"annoteren"/run/filename,raw))
        require(len(set(chosen)) == SAMPLES and chosen == sorted(chosen),
                "Chronological/independent selection invalid.")
        frozen.append({"runId":run,"sourceTraceSha256":sha,
                       "sourceManifestSha256":digest(manifest_bytes),
                       "totalOriginalFrames":len(lines),"images":records})
    require(len(trace_hashes)==3, "Not three distinct source whole-game sessions.")
    plan={"schemaVersion":1,"seed":SEEDS[role],"sourceRole":role,
          "partition":"development" if role=="development" else "untouched-prospective",
          "runIds":list(expected),"numOriginalImages":54,
          "uniqueImageDigestCount":len(selected_digests),
          "selectionProtocol":"SHA-seeded fixed 18 chronological frame bins; no labels, outcome, phase, position or agent guesses",
          "humanGroundTruth":"ALL_UNKNOWN","trainingAuthorized":role=="development",
          "prospectiveSealIntact":role=="prospective","runs":frozen}
    all_files.append((destination/"study-manifest.json",
                      (json.dumps(plan,indent=2)+"\n").encode()))
    if role=="development":
        for entry in frozen:
            run=entry["runId"]
            html=review_html(run,entry["images"])
            all_files.append((destination/"annoteren"/run/"index.html",html.encode()))
        all_files.append((destination/"index.html",(
            '<!doctype html><html lang="nl"><meta charset="utf-8"><meta name="viewport" '
            'content="width=device-width, initial-scale=1"><title>ARGUS M2.23</title><body>'
            '<h1>ARGUS M2.23 — nieuw ontwikkelonderzoek</h1>'+
            "".join(f'<p><a href="annoteren/{run}/">{run}: 18 originele screenshots</a></p>'
                    for run in expected)+"</body></html>").encode()))
    for filename,raw in all_files:
        filename.parent.mkdir(parents=True,exist_ok=True)
        filename.write_bytes(raw)
    require(len(list(destination.rglob("*.json"))) == 1 and
            len(list(destination.rglob("*.png"))) == 54 and
            len(list(destination.rglob("*.jsonl"))) == 0, "Unexpected non-source material.")
    print(f"PASS M2.23 {role}: three authentic separate 390x844 source runs; "
          "54 frozen frames; zero human truth/trace JSONL; "
          +("PRIVATE BLIND REVIEW" if role=="development" else "SEALED, NO REVIEW OR TRAINING"))
    return plan


def review_html(run, images):
    from tools.m220.point_cohort import TEMPLATE
    code=TEMPLATE.read_text()
    replacements=[
        ('/^dev-[fgh]$/','/^dev-[lmn]$/'),
        ('study.images.length!==12','study.images.length!==18'),
        ('a.length===12','a.length===images.length'),
        ('idx===11','idx===images.length-1'),
        ('if(idx<11)','if(idx<images.length-1)'),
        ('" / 12 · "+','" / "+images.length+" · "+'),
        ('"argus-m218-player-point-"','"argus-m223-player-point-"'),
        ('"m218-"','"m223-"'),
        ('ARGUS M2.18 · ','ARGUS M2.23 · ')
    ]
    for previous,next_value in replacements:
        require(code.count(previous)==1,"Published blank reviewer UI no longer compatible: "+previous)
        code=code.replace(previous,next_value)
    require(code.count("/*ARGUS_POINT_STUDY*/")==1 and
            "fetch(" not in code and "playerX" not in code and "threatLocations" not in code,
            "Reviewer could leak human data or ARGUS guesses.")
    study={"schemaVersion":1,"seed":SEEDS["development"],"sourceSeed":SEEDS["development"],
           "runId":run,"partition":"development","images":images}
    return code.replace("/*ARGUS_POINT_STUDY*/",json.dumps(study,separators=(",",":")))


def main():
    p=argparse.ArgumentParser()
    p.add_argument("--role",choices=("development","prospective"),required=True)
    p.add_argument("--runs",type=Path,required=True)
    p.add_argument("--output",type=Path,required=True)
    a=p.parse_args()
    original_frames(a.runs,a.role,a.output)


if __name__=="__main__":
    main()
