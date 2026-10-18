#!/usr/bin/env python3
"""M2.27b preservation: verify authentic prior successful R/T, reject invalid S.

Never silently convert an unsuccessful browser process to a valid game run,
synthesize screenshots, rewrite old provenance or touch prospective O/P/Q.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path

PNG = b"\x89PNG\r\n\x1a\n"
SUCCESSFUL_ORIGINAL_RUNS = ("dev-r", "dev-t")
ALL_DEVELOPMENT = ("dev-r", "dev-s", "dev-t")


def require(ok, reason):
    if not ok:
        raise ValueError(reason)


def verify_source(root: Path, run: str):
    require(run in ALL_DEVELOPMENT, "Only R/S/T development original screenshot runs are allowed.")
    folder = root/run
    require(folder.is_dir() and not folder.is_symlink(),
            "Missing source browser session: "+run)
    metadata_path = folder/"run-manifest.json"
    trace_path = folder/"trace.jsonl"
    require(metadata_path.is_file() and trace_path.is_file(),
            "Missing authoritative source trace or manifest.")
    metadata = json.loads(metadata_path.read_text())
    raw = trace_path.read_bytes()
    lines = [json.loads(line) for line in raw.splitlines()]
    require(metadata.get("schemaVersion") == 1 and metadata.get("runId") == run and
            metadata.get("partition") == "development" and
            metadata.get("runnerExitCode") == 0 and
            metadata.get("visualEvidenceGate") is True and
            metadata.get("actualGameOutcome") == "unknown" and
            metadata.get("viewport") == "390x844" and
            metadata.get("traceSha256") == hashlib.sha256(raw).hexdigest() and
            type(metadata.get("frames")) is int and
            metadata["frames"] == len(lines) and 36 <= len(lines) <= 180,
            "INVALID original browser provenance: do not relabel failed run as success.")
    for i, event in enumerate(lines):
        name = f"frame-{i:04d}.png"
        require(event.get("step") == i and event.get("screenshot") == name,
                "Original game trace is not chronological.")
        path = folder/name
        require(path.is_file() and not path.is_symlink(),
                "Missing actual original screenshot in source trace.")
        with path.open("rb") as stream:
            header = stream.read(24)
        require(len(header) == 24 and header.startswith(PNG) and
                struct.unpack(">II", header[16:24]) == (390, 844),
                "Original screenshot dimensions or PNG signature changed.")
    return {"run": run, "frames": len(lines),
            "traceSha256": metadata["traceSha256"],
            "runnerExitCode": 0, "humanTruth": "UNKNOWN"}


def main():
    p=argparse.ArgumentParser()
    p.add_argument("--runs",type=Path,required=True)
    p.add_argument("--include-s",action="store_true")
    args=p.parse_args()
    runs = ALL_DEVELOPMENT if args.include_s else SUCCESSFUL_ORIGINAL_RUNS
    results=[verify_source(args.runs,run) for run in runs]
    require(len({item["traceSha256"] for item in results}) == len(results),
            "Original source process reuse detected; not independent gameplay.")
    for entry in results:
        print("PASS authentic original, no human game truth:",entry)


if __name__=="__main__":
    main()
