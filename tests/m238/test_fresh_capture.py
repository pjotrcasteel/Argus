"""M2.38 synthetic-only real-PNG provenance, blinded selection and privacy."""
import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from PIL import Image

from tools.m238 import fresh_capture as m


class FreshCaptureSyntheticTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.captures = self.root / "captures"
        self.profile = self.root / "profile.json"
        self.profile.write_text('{"fake":true}')
        for run in m.RUNS:
            folder = self.captures / run
            folder.mkdir(parents=True)
            rows = []
            for step in range(40):
                frame = f"frame-{step:04d}.png"
                png = Image.new("RGB", (390,844), (23,32,35))
                png.putpixel((step,20), (step+1,sum(map(ord,run)) % 250,40))
                png.save(folder / frame)
                rows.append({"step":step, "screenshot":frame,
                             "evidence":{"phase":"unknown","independentRun":run}})
            (folder / "trace.jsonl").write_text(
                "".join(json.dumps(r)+"\n" for r in rows))
            (folder / "summary.json").write_text(json.dumps(
                {"frames":len(rows),"meetsVisualCombatGate":True}))
            m.record(folder, run, "2026-10-18T20:40:00Z", 0, "test-argus-sha",
                     self.profile)

    def tearDown(self):
        self.temp.cleanup()

    def test_real_original_png_integrity_and_version_not_invented(self):
        for run in m.RUNS:
            evidence = m.verify_run(self.captures / run, run)
            self.assertEqual(40,evidence["frames"])
            self.assertIsNone(evidence["meta"]["gameVersion"])
            self.assertFalse(evidence["meta"]["versionAttestedByPublicGame"])
            self.assertEqual(40,len(evidence["originalPngSha256"]))
            self.assertEqual(64,len(evidence["traceSha256"]))

    def test_full_three_run_original_blind_freeze_no_private_predictions(self):
        frozen = self.root / "private-freeze"
        result = m.freeze(self.captures,frozen)
        self.assertEqual("NOT_EVALUATED",result["researchStatus"])
        self.assertEqual(54,result["originalTargetCount"])
        self.assertEqual(54,result["originalImmediatePredecessorCount"])
        self.assertFalse(result["publicPublicationAuthorized"])
        self.assertFalse(result["prospectiveSealedSourcesAccessed"])
        self.assertIsNone(result["gameVersion"])
        self.assertEqual(108,len(list(frozen.rglob("*.png"))))
        self.assertEqual(1,len(list(frozen.rglob("*.json"))))
        self.assertFalse(list(frozen.rglob("*.jsonl")))
        self.assertFalse(list(frozen.rglob("labels.json")))
        for r in result["runs"]:
            self.assertEqual(18,len(r["images"]))
            steps = [a["step"] for a in r["images"]]
            self.assertEqual(sorted(set(steps)),steps)
            for image,prior in zip(r["images"],r["immediatePriorOriginalFrames"]):
                self.assertEqual(image["step"]-1,prior["priorStep"])
                original = frozen/"annoteren"/r["runId"]/image["file"]
                earlier = frozen/prior["file"]
                self.assertEqual(image["sha256"],hashlib.sha256(original.read_bytes()).hexdigest())
                self.assertEqual(prior["sha256"],hashlib.sha256(earlier.read_bytes()).hexdigest())
            html = (frozen/"annoteren"/r["runId"]/"index.html").read_text()
            self.assertIn(r["runId"],html)
            self.assertIn("/^m238-[abc]$/",html)
            self.assertNotIn("playerX",html)
            self.assertNotIn("fetch(",html)
        with self.assertRaisesRegex(ValueError,"Never replace"):
            m.freeze(self.captures,frozen)

    def test_corrupted_bytes_disallowed(self):
        folder=self.captures / "m238-b"
        (folder/"frame-0007.png").write_bytes(b"not a PNG")
        with self.assertRaises((ValueError,OSError)):
            m.verify_run(folder,"m238-b")

    def test_failed_runner_and_game_without_combat_are_not_fabricated(self):
        file = self.captures/"m238-c"/"run-manifest.json"
        meta=json.loads(file.read_text())
        meta["runnerExitCode"]=1
        file.write_text(json.dumps(meta))
        with self.assertRaisesRegex(ValueError,"NOT_READY"):
            m.freeze(self.captures,self.root/"not-ready")
        meta["runnerExitCode"]=0
        meta["visualEvidenceGate"]=False
        file.write_text(json.dumps(meta))
        with self.assertRaisesRegex(ValueError,"NOT_READY"):
            m.verify_run(self.captures/"m238-c","m238-c")

    def test_changed_source_trace_sha_rejected(self):
        folder=self.captures/"m238-a"
        trace=folder/"trace.jsonl"
        trace.write_text(trace.read_text()+"\n")
        with self.assertRaisesRegex(ValueError,"NOT_READY"):
            m.verify_run(folder,"m238-a")

    def test_unauthenticated_version_is_explicitly_unknown(self):
        file=self.captures/"m238-b"/"run-manifest.json"
        record=json.loads(file.read_text())
        record["gameVersion"]="0.0.118"
        file.write_text(json.dumps(record))
        with self.assertRaisesRegex(ValueError,"NOT_READY"):
            m.verify_run(self.captures/"m238-b","m238-b")

    def test_duplicate_whole_source_traces_not_independent(self):
        source=self.captures/"m238-a"/"trace.jsonl"
        dest=self.captures/"m238-b"/"trace.jsonl"
        dest.write_bytes(source.read_bytes())
        folder=self.captures/"m238-b"
        summary=json.loads((folder/"summary.json").read_text())
        m.record(folder,"m238-b","2026-10-18T20:40:00Z",0,"test-argus-sha",self.profile)
        with self.assertRaisesRegex(ValueError,"Duplicate whole"):
            m.freeze(self.captures,self.root/"dup-freeze")


if __name__=="__main__":
    unittest.main()
