#!/usr/bin/env python3
"""M2.32 provenance gate: original external browser screenshots, no player oracle.

Accept only independent new U/V/W development runs. A failed real runner,
missing PNG, duplicate trace or fabricated frame count MUST fail closed.
"""
import argparse
import datetime
import hashlib
import json
import re
import struct
from pathlib import Path

RUNS = ("dev-u", "dev-v", "dev-w")
PNG = b"\x89PNG\r\n\x1a\n"
URL = "https://pjotrcasteel.github.io/Wyrmforge/"


def require(ok, reason):
    if not ok:
        raise ValueError(reason)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def write_manifest(folder, run, started, exit_code, revision, profile):
    require(run in RUNS and type(exit_code) is int and exit_code >= 0,
            "Unknown study run or malformed actual browser process exit.")
    trace = folder / "trace.jsonl"
    summary_file = folder / "summary.json"
    summary = json.loads(summary_file.read_text()) if summary_file.is_file() else {}
    require(profile.is_file(), "Original browser profile missing.")
    record = {"schemaVersion": 1, "partition": "development", "runId": run,
              "captureMode": "unmodified independent external browser screenshot and normal player inputs",
              "startedAtUtc": started,
              "finishedAtUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(timespec="seconds"),
              "runnerExitCode": exit_code, "actualGameOutcome": "unknown", "gameVersion": None,
              "gameUrl": URL, "profileSha256": sha(profile.read_bytes()),
              "traceSha256": sha(trace.read_bytes()) if trace.is_file() else None,
              "policyVersion": revision, "viewport": "390x844",
              "frames": summary.get("frames"),
              "visualEvidenceGate": summary.get("meetsVisualCombatGate")}
    folder.mkdir(parents=True, exist_ok=True)
    (folder/"run-manifest.json").write_text(json.dumps(record, sort_keys=True, indent=2)+"\n")
    return record


def verify_run(folder, run):
    require(run in RUNS and folder.is_dir() and not folder.is_symlink(),
            "Missing or unauthorized independent development source.")
    meta_file, trace_file = folder/"run-manifest.json", folder/"trace.jsonl"
    require(meta_file.is_file() and trace_file.is_file(), "Real trace/manifest not captured.")
    meta = json.loads(meta_file.read_text())
    raw = trace_file.read_bytes()
    try:
        frames = [json.loads(line) for line in raw.splitlines()]
    except (ValueError, UnicodeDecodeError) as error:
        raise ValueError("Malformed original browser JSONL trace.") from error
    require(meta.get("schemaVersion") == 1 and meta.get("partition") == "development" and
            meta.get("runId") == run and meta.get("runnerExitCode") == 0 and
            meta.get("visualEvidenceGate") is True and
            meta.get("actualGameOutcome") == "unknown" and meta.get("viewport") == "390x844" and
            meta.get("gameUrl") == URL and
            isinstance(meta.get("profileSha256"), str) and
            bool(re.fullmatch(r"[a-f0-9]{64}",meta["profileSha256"])) and
            meta.get("traceSha256") == sha(raw) and
            type(meta.get("frames")) is int and meta["frames"] == len(frames) and
            36 <= len(frames) <= 180,
            "Invalid actual real-game capture: never repair a failed process by changing metadata.")
    for i, frame in enumerate(frames):
        filename = f"frame-{i:04d}.png"
        require(frame.get("step") == i and frame.get("screenshot") == filename,
                "Original game steps or screenshots not chronological.")
        image = folder/filename
        require(image.is_file() and not image.is_symlink(), "Missing genuine original PNG.")
        with image.open("rb") as source:
            head = source.read(24)
        require(len(head) == 24 and head[:8] == PNG and
                struct.unpack(">II", head[16:24]) == (390, 844),
                "Original screenshot invalid 390x844 PNG geometry.")
    return {"run":run, "frames":len(frames), "traceSha256":sha(raw),
            "verifiedActualProcessExit":0, "independentHumanLabels":"NONE"}


def verify_all(root):
    results = [verify_run(root/run, run) for run in RUNS]
    require(len({r["traceSha256"] for r in results}) == 3,
            "Three independent whole-game original screenshot traces are required.")
    return results


def main():
    p = argparse.ArgumentParser()
    commands = p.add_subparsers(dest="cmd", required=True)
    v = commands.add_parser("verify")
    v.add_argument("--runs", required=True, type=Path)
    w = commands.add_parser("record")
    w.add_argument("--run", required=True, choices=RUNS)
    w.add_argument("--folder", required=True, type=Path)
    w.add_argument("--started", required=True)
    w.add_argument("--exit", required=True, type=int)
    w.add_argument("--revision", required=True)
    w.add_argument("--profile", required=True, type=Path)
    a = p.parse_args()
    if a.cmd == "verify":
        for result in verify_all(a.runs):
            print("PASS VERIFIED genuine original development capture:", result)
    else:
        print(json.dumps(write_manifest(a.folder, a.run, a.started, a.exit, a.revision, a.profile)))


if __name__ == "__main__":
    main()
