"""Synthetic R/S/T screenshots only. No prospective, user references or real game pixels."""
import hashlib
import json
import struct
import sys
import tempfile
import unittest
import zlib
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from tools.m227 import freeze_temporal_context as m227


def png():
    def chunk(tag, body):
        return struct.pack(">I", len(body)) + tag + body + struct.pack(">I", zlib.crc32(tag+body)&0xffffffff)
    row = b"\x00" + bytes((30, 70, 90))*390
    return (m227.PNG + chunk(b"IHDR", struct.pack(">IIBBBBB", 390, 844, 8, 2, 0, 0, 0)) +
            chunk(b"IDAT", zlib.compress(row*844, 7)) + chunk(b"IEND", b""))


class TemporalFreezeTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.root = Path(self.folder.name)
        self.inputs = self.root/"synthetic"
        self.inputs.mkdir()
        raw = png()
        for run in m227.RUNS:
            folder = self.inputs/run
            folder.mkdir()
            lines = []
            for step in range(42):
                # Source PNG bytes are genuine PNG but all synthetic.
                (folder/f"frame-{step:04d}.png").write_bytes(raw)
                lines.append({"step": step, "screenshot": f"frame-{step:04d}.png",
                              "fictionalTraceRun": run})
            trace = ("\n".join(json.dumps(x) for x in lines) + "\n").encode()
            (folder/"trace.jsonl").write_bytes(trace)
            manifest = {"schemaVersion": 1, "partition": "development",
                        "runId": run, "runnerExitCode": 0, "viewport": "390x844",
                        "visualEvidenceGate": True, "actualGameOutcome": "unknown",
                        "frames": 42, "traceSha256": hashlib.sha256(trace).hexdigest()}
            (folder/"run-manifest.json").write_text(json.dumps(manifest))

    def tearDown(self):
        self.folder.cleanup()

    def test_three_authentic_source_groups_and_immediate_prior_image_only(self):
        output = self.root/"review"
        study = m227.freeze(self.inputs, output)
        self.assertEqual(study["runIds"], list(m227.RUNS))
        self.assertEqual(study["targetsPerRun"], 18)
        self.assertEqual(len(list(output.rglob("*.png"))), 108)
        self.assertEqual(len(list(output.rglob("annoteren/*/index.html"))), 3)
        self.assertFalse(list(output.rglob("*.jsonl")))
        self.assertEqual(study["humanGroundTruth"], "ALL_UNKNOWN")
        self.assertFalse(study["prospectiveOriginalsAccessed"])
        for run in study["runs"]:
            self.assertEqual(18, len(run["images"]))
            self.assertEqual(18, len(run["immediatePriorOriginalFrames"]))
            self.assertTrue(all(p["priorStep"]+1 == p["targetStep"]
                                for p in run["immediatePriorOriginalFrames"]))
            self.assertTrue(all(p["targetStep"]>=1 for p in run["immediatePriorOriginalFrames"]))
            self.assertEqual([x["step"] for x in run["images"]],
                             sorted(set(x["step"] for x in run["images"])))
            page = (output/"annoteren"/run["runId"]/"index.html").read_text()
            self.assertIn("m227-player-point-", page)
            self.assertNotIn('"state":"visible"', page)
            self.assertNotIn("fetch(", page)
            self.assertNotIn("run-01", page)
            self.assertNotIn("dev-o", page)
        with self.assertRaisesRegex(ValueError, "Never overwrite"):
            m227.freeze(self.inputs, output)

    def test_corrupt_selected_context_and_source_trace_fail_closed(self):
        seed = m227.SEED
        h = hashlib.sha256(f"{seed}\ndev-s\n0".encode()).digest()
        step = 1 + int.from_bytes(h[:8],"big") % (42//18-1)
        context = self.inputs/"dev-s"/f"frame-{step-1:04d}.png"
        original = context.read_bytes()
        context.write_bytes(b"corrupt")
        with self.assertRaisesRegex(ValueError, "original PNG"):
            m227.freeze(self.inputs, self.root/"rejected")
        context.write_bytes(original)
        raw = self.inputs/"dev-s"/"trace.jsonl"
        raw.write_bytes(raw.read_bytes()+b"falsified")
        with self.assertRaisesRegex(ValueError, "trace changed"):
            m227.freeze(self.inputs, self.root/"rejected2")

    def test_refuse_nonindependent_browser_session(self):
        other = self.inputs/"dev-t"/"trace.jsonl"
        source = self.inputs/"dev-r"/"trace.jsonl"
        other.write_bytes(source.read_bytes())
        m = self.inputs/"dev-t"/"run-manifest.json"
        info = json.loads(m.read_text())
        info["traceSha256"] = hashlib.sha256(other.read_bytes()).hexdigest()
        m.write_text(json.dumps(info))
        with self.assertRaisesRegex(ValueError, "duplicate independent run"):
            m227.freeze(self.inputs, self.root/"rejected3")


if __name__ == "__main__":
    unittest.main()
