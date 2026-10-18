"""Synthetic-only M2.27b recovery gating; no actual owner or prospective files."""
import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0,str(Path(__file__).resolve().parents[2]))
sys.path.insert(0,str(Path(__file__).resolve().parents[2]/"tests/m227"))
from test_temporal_freeze import TemporalFreezeTests
from tools.m227 import recovery_gate as gate


class PreservedOriginalTests(unittest.TestCase):
    def setUp(self):
        self.fake=TemporalFreezeTests()
        self.fake.setUp()

    def tearDown(self):
        self.fake.tearDown()

    def test_only_genuinely_complete_independent_original_source_is_admitted(self):
        for run in ("dev-r","dev-t"):
            result=gate.verify_source(self.fake.inputs,run)
            self.assertEqual(result["frames"],42)
            self.assertEqual(result["humanTruth"],"UNKNOWN")
            self.assertEqual(result["runnerExitCode"],0)
        self.assertNotEqual(
            gate.verify_source(self.fake.inputs,"dev-r")["traceSha256"],
            gate.verify_source(self.fake.inputs,"dev-t")["traceSha256"])

    def test_failed_original_dev_s_cannot_be_silently_relabelled_success(self):
        p=self.fake.inputs/"dev-s"/"run-manifest.json"
        doc=json.loads(p.read_text())
        doc["runnerExitCode"]=1
        doc["visualEvidenceGate"]=None
        doc["frames"]=None
        p.write_text(json.dumps(doc))
        with self.assertRaisesRegex(ValueError,"INVALID original browser provenance"):
            gate.verify_source(self.fake.inputs,"dev-s")
        self.assertEqual(gate.verify_source(self.fake.inputs,"dev-r")["frames"],42)

    def test_corrupt_source_trace_or_screenshot_never_accepted(self):
        p=self.fake.inputs/"dev-r"/"trace.jsonl"
        p.write_bytes(p.read_bytes()+b"silently appended invalid trace")
        with self.assertRaisesRegex(ValueError,"INVALID original browser provenance"):
            gate.verify_source(self.fake.inputs,"dev-r")
        p=self.fake.inputs/"dev-t"/"frame-0004.png"
        p.write_bytes(b"not an authentic screenshot")
        with self.assertRaisesRegex(ValueError,"Original screenshot dimensions"):
            gate.verify_source(self.fake.inputs,"dev-t")

    def test_sealed_and_original_heldout_names_refused(self):
        for run in ("dev-o","dev-p","dev-q","run-01"):
            with self.assertRaisesRegex(ValueError,"Only R/S/T"):
                gate.verify_source(self.fake.inputs,run)


if __name__=="__main__":
    unittest.main()
