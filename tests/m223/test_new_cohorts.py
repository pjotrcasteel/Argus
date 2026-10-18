"""All content is synthetic. Real player labels, images or predictions must never enter CI."""
import hashlib
import json
import struct
import sys
import tempfile
import unittest
import zlib
from pathlib import Path

sys.path.insert(0,str(Path(__file__).resolve().parents[2]))
from tools.m223 import freeze_new_cohorts as m223


def valid_png():
    def chunk(kind,raw):
        return (struct.pack(">I",len(raw))+kind+raw+
                struct.pack(">I",zlib.crc32(kind+raw)&0xffffffff))
    row=b"\x00"+bytes((57,66,75))*390
    return (m223.PNG+
            chunk(b"IHDR",struct.pack(">IIBBBBB",390,844,8,2,0,0,0))+
            chunk(b"IDAT",zlib.compress(row*844,7))+chunk(b"IEND",b""))


class CohortTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix="argus-m223-synthetic-only-")
        self.root=Path(self.temp.name)
        self.inputs=self.root/"runs"
        png=valid_png()
        for run in (*m223.DEVELOPMENT,*m223.PROSPECTIVE):
            folder=self.inputs/run
            folder.mkdir(parents=True)
            frames=[]
            for step in range(40):
                (folder/f"frame-{step:04d}.png").write_bytes(png)
                frames.append({"step":step,"screenshot":f"frame-{step:04d}.png",
                               "fictitiousSyntheticSession":run})
            trace=("\n".join(json.dumps(x) for x in frames)+"\n").encode()
            (folder/"trace.jsonl").write_bytes(trace)
            (folder/"run-manifest.json").write_text(json.dumps({
                "schemaVersion":1,"partition":"development","runId":run,
                "runnerExitCode":0,"actualGameOutcome":"unknown",
                "viewport":"390x844","visualEvidenceGate":True,
                "frames":40,"traceSha256":hashlib.sha256(trace).hexdigest()}))

    def tearDown(self):
        self.temp.cleanup()

    def test_both_roles_are_disjoint_in_ids_images_and_reviewers(self):
        dev_out=self.root/"dev"
        test_out=self.root/"prospective"
        dev=m223.original_frames(self.inputs,"development",dev_out)
        hold=m223.original_frames(self.inputs,"prospective",test_out)
        self.assertEqual(dev["runIds"],list(m223.DEVELOPMENT))
        self.assertEqual(hold["runIds"],list(m223.PROSPECTIVE))
        self.assertEqual(dev["humanGroundTruth"],"ALL_UNKNOWN")
        self.assertEqual(hold["humanGroundTruth"],"ALL_UNKNOWN")
        self.assertFalse(dev["prospectiveSealIntact"])
        self.assertTrue(hold["prospectiveSealIntact"])
        self.assertFalse(hold["trainingAuthorized"])
        self.assertEqual(len(list(dev_out.rglob("frame-*.png"))),54)
        self.assertEqual(len(list(test_out.rglob("frame-*.png"))),54)
        self.assertEqual(len(list(dev_out.rglob("*/index.html"))),3)
        self.assertFalse(list(test_out.rglob("*.html")))
        self.assertEqual(len(list(test_out.rglob("*.json"))),1)
        self.assertFalse(list(test_out.rglob("*.jsonl")))
        self.assertNotIn("dev-o",json.dumps(dev))
        self.assertNotIn("dev-l",json.dumps(hold))
        for run in dev["runs"]:
            self.assertEqual([x["step"] for x in run["images"]],
                             sorted(set(x["step"] for x in run["images"])))
            page=(dev_out/"annoteren"/run["runId"]/"index.html").read_text()
            self.assertIn("m223-player-point-",page)
            self.assertNotIn("run-01",page)
            self.assertNotIn('"state":"visible"',page)
            self.assertNotIn("fetch(",page)

    def test_refuse_overwrite_and_broken_origin(self):
        dev_out=self.root/"fresh"
        m223.original_frames(self.inputs,"development",dev_out)
        with self.assertRaisesRegex(ValueError,"Refuse"):
            m223.original_frames(self.inputs,"development",dev_out)
        source=self.inputs/"dev-l"/"run-manifest.json"
        p=json.loads(source.read_text())
        p["visualEvidenceGate"]=False
        source.write_text(json.dumps(p))
        with self.assertRaisesRegex(ValueError,"visual evidence"):
            m223.original_frames(self.inputs,"development",self.root/"broken")

    def test_immutable_sample_rejects_corrupt_source_image_and_trace(self):
        # Corrupt the *predeclared selected* image, not an arbitrary unused frame.
        first_bin_hash=hashlib.sha256(
            (m223.SEEDS["development"]+"\ndev-n\n0").encode()).digest()
        step=int.from_bytes(first_bin_hash[:8],"big") % (40//18)
        p=self.inputs/"dev-n"/f"frame-{step:04d}.png"
        p.write_bytes(b"bad png")
        with self.assertRaisesRegex(ValueError,"PNG"):
            m223.original_frames(self.inputs,"development",self.root/"failed")
        p.write_bytes(valid_png())
        p=self.inputs/"dev-n"/"trace.jsonl"
        p.write_bytes(p.read_bytes()+b"extra")
        with self.assertRaisesRegex(ValueError,"source whole-game trace"):
            m223.original_frames(self.inputs,"development",self.root/"failed2")

if __name__=="__main__":
    unittest.main()
