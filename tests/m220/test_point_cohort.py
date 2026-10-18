"""Artificial-only M2.20 run sampling / invisible-data / private point admission tests."""
import hashlib
import importlib.util
import json
import struct
import tempfile
import unittest
import zlib
from pathlib import Path

SOURCE = Path(__file__).resolve().parents[2] / "tools/m220/point_cohort.py"
spec = importlib.util.spec_from_file_location("m220", SOURCE)
m220 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m220)


def png(color):
    def chunk(t, v):
        return struct.pack(">I", len(v)) + t + v + struct.pack(">I", zlib.crc32(t + v) & 0xffffffff)
    row = b"\0" + bytes(color) * 390
    return (m220.PNG + chunk(b"IHDR", struct.pack(">IIBBBBB",390,844,8,2,0,0,0))
            + chunk(b"IDAT", zlib.compress(row * 844, 4)) + chunk(b"IEND",b""))


class Fixture:
    def __init__(self):
        self.temp = tempfile.TemporaryDirectory(prefix="argus-m220-synthetic-")
        self.root = Path(self.temp.name)
        self.runs = self.root / "sources"
        self.site = self.root / "review"
        for j, run in enumerate(m220.RUNS):
            folder = self.runs / run
            folder.mkdir(parents=True)
            rows = []
            for step in range(42):
                (folder / f"frame-{step:04d}.png").write_bytes(png(((15+j*36+step)%255,55,99)))
                rows.append({"step":step,"screenshot":f"frame-{step:04d}.png","syntheticSession":run,
                    "evidence":{"phase":("exploring" if step%3 else "unknown"),
                                "combat":{"playerX":195, "playerY":422}}})
            self.trace(run,rows)
            self.owner_manifest(run,j)

    def trace(self,run,rows):
        folder=self.runs / run
        (folder/"trace.jsonl").write_text("\n".join(json.dumps(r) for r in rows)+"\n")

    def owner_manifest(self,run,index):
        folder=self.runs/run
        trace=m220.sha((folder/"trace.jsonl").read_bytes())
        (folder/"run-manifest.json").write_text(json.dumps({
            "schemaVersion":1,"partition":"development","runId":run,
            "runnerExitCode":0,"actualGameOutcome":"unknown",
            "viewport":"390x844","visualEvidenceGate":True,"frames":42,
            "traceSha256":trace,"startedAtUtc":f"2026-10-18T09:0{index}:00Z",
            "policyVersion":"test-synthetic-only"}))

    def close(self):
        self.temp.cleanup()


class CohortTests(unittest.TestCase):
    def setUp(self):self.f=Fixture()
    def tearDown(self):self.f.close()

    def test_original_images_blind_and_time_binned(self):
        p=m220.build(self.f.runs,self.f.site)
        self.assertEqual(list(m220.RUNS),p["runIds"])
        self.assertEqual(54,sum(len(r["images"]) for r in p["runs"]))
        self.assertEqual(54,len(list(self.f.site.glob("annoteren/*/frame-*.png"))))
        self.assertNotIn("run-01",json.dumps(p))
        for run in p["runs"]:
            self.assertEqual(run["selectionSteps"],sorted(set(run["selectionSteps"])))
            page=(self.f.site/"annoteren"/run["runId"]/"index.html").read_text()
            self.assertIn('study.images.length!==18',page)
            self.assertIn('a.length===images.length',page)
            self.assertIn('idx===images.length-1',page)
            self.assertIn('"argus-m220-player-point-"',page)
            self.assertNotIn('"playerX"',page)
            self.assertNotIn('"state":"visible"',page)
            for img in run["images"]:
                original=(self.f.runs/run["runId"]/img["file"]).read_bytes()
                copy=(self.f.site/"annoteren"/run["runId"]/img["file"]).read_bytes()
                self.assertEqual(original,copy)
                self.assertEqual(img["sha256"],m220.sha(original))

    def test_selection_not_steered_by_agent_prediction_or_phase(self):
        baseline=m220.build(self.f.runs,self.f.site)
        steps=[r["selectionSteps"] for r in baseline["runs"]]
        # Recreate from the exact original PNGs; change only misleading AI/phase evidence.
        for run in m220.RUNS:
            folder=self.f.runs/run
            trace=[json.loads(t) for t in (folder/"trace.jsonl").read_text().splitlines()]
            for row in trace:
                row["evidence"]={"phase":"terminal","combat":{"playerX":-1000,"playerY":999}}
            self.f.trace(run,trace)
            self.f.owner_manifest(run,list(m220.RUNS).index(run))
        alternate=m220.build(self.f.runs,self.f.root/"alternate")
        self.assertEqual(steps,[r["selectionSteps"] for r in alternate["runs"]])
        self.assertEqual([r["images"] for r in baseline["runs"]],
                         [r["images"] for r in alternate["runs"]])

    def test_private_audit_fail_closed_and_quality_gate(self):
        plan=m220.build(self.f.runs,self.f.site)
        files=[]
        for run in plan["runs"]:
            rows=[]
            for index,item in enumerate(run["images"]):
                player=({"state":"visible", "center":{"x":80+index*8,"y":410}}
                        if index<5 else {"state":"absent","center":None}
                        if index<9 else {"state":"unknown","center":None})
                rows.append({"step":item["step"],"screenshotSha256":item["sha256"],"player":player})
            data={"schemaVersion":1,"studySeed":m220.SEED,"sourceStudySeed":m220.SEED,
                  "runId":run["runId"],"partition":"development","frames":rows}
            f=self.f.root/(run["runId"]+".json")
            f.write_text(json.dumps(data))
            files.append(f)
        original=[f.read_bytes() for f in files]
        report=m220.validate_owner(self.f.site,files)
        self.assertEqual("STRUCTURALLY_ELIGIBLE",report["status"])
        self.assertEqual([{"visible":5,"absent":4,"unknown":9}]*3,
                         [r["counts"] for r in report["runs"].values()])
        self.assertEqual(original,[f.read_bytes() for f in files])
        bad=json.loads(files[0].read_text())
        bad["frames"][0]["screenshotSha256"]="tamp"
        files[0].write_text(json.dumps(bad))
        with self.assertRaisesRegex(ValueError,"frozen source"):m220.validate_owner(self.f.site,files)
        bad["frames"][0]["screenshotSha256"]=plan["runs"][0]["images"][0]["sha256"]
        bad["runId"]="run-01"
        files[0].write_text(json.dumps(bad))
        with self.assertRaisesRegex(ValueError,"Invalid or duplicate"):m220.validate_owner(self.f.site,files)

    def test_duplicate_trace_and_owner_label_in_capture_rejected(self):
        trace=(self.f.runs/"dev-i"/"trace.jsonl").read_bytes()
        folder=self.f.runs/"dev-j"
        (folder/"trace.jsonl").write_bytes(trace)
        self.f.owner_manifest("dev-j",1)
        with self.assertRaisesRegex(ValueError,"Duplicate"):m220.build(self.f.runs,self.f.site)
        (folder/"trace.jsonl").write_text("not json")
        (self.f.runs/"dev-i"/"labels.json").write_text('{"human":"private"}')
        with self.assertRaisesRegex(ValueError,"Owner labels"):m220.build(self.f.runs,self.f.site)


if __name__=="__main__":unittest.main()
