"""M2.18 artificial-only immutable source and human-point admission tests."""
import hashlib
import importlib.util
import json
import struct
import tempfile
import unittest
from pathlib import Path
from zipfile import ZipFile

SCRIPT = Path(__file__).resolve().parents[2] / "tools/m218/player_point_study.py"
spec = importlib.util.spec_from_file_location("argus_m218", SCRIPT)
m218 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m218)


class Fixture:
    def __init__(self):
        self.t = tempfile.TemporaryDirectory(prefix="m218-artificial-")
        self.root = Path(self.t.name)
        self.archive = self.root / "artificial-source.zip"
        self.site = self.root / "_site"
        self.site.mkdir()
        self.owner = {}
        study = []
        with ZipFile(self.archive, "w") as zip_file:
            zip_file.writestr("index.html", "SYNTHETIC")
            for run, char in zip(m218.RUNS, "abc"):
                images, probes = [], []
                folder = self.site / "annoteren" / run
                folder.mkdir(parents=True)
                (folder / "index.html").write_text("SYNTHETIC EMPTY original reviewer.")
                zip_file.writestr(f"annoteren/{run}/index.html", "SYNTHETIC original reviewer.")
                for step in range(12):
                    png = b"\x89PNG\r\n\x1a\n" + b"\0"*8 + struct.pack(">II",390,844) + b"NOT AN ACTUAL PNG" + bytes([step])
                    name = f"frame-{step:04d}.png"
                    zip_file.writestr(f"annoteren/{run}/{name}", png)
                    (folder / name).write_bytes(png)
                    images.append({"step":step,"file":name,"sha256":hashlib.sha256(png).hexdigest()})
                    for role, x, y in (("center-anchor",195,422),("spatial-control",72,180)):
                        probes.append({"sampleId":f"{run}-step{step:04d}-{role}",
                                       "step":step,"x":x,"y":y,"radius":26,"role":role,"label":"unknown"})
                study.append({"schemaVersion":1,"runId":run,"partition":"development",
                              "traceSha256":char*64,"images":images,"candidateCount":24,"candidates":probes})
                export = {"schemaVersion":1,"studySeed":m218.POINT_SEED,
                          "sourceStudySeed":m218.SOURCE_SEED,"runId":run,"partition":"development",
                          "frames":[{"step":i["step"],"screenshotSha256":i["sha256"],
                            "player":{"state":"visible","center":{"x":120+step*8,"y":270+step*13}}
                                if step<3 else {"state":"absent","center":None}
                                if step<6 else {"state":"unknown","center":None}}
                           for step,i in enumerate(images)]}
                file = self.root / f"{run}.json"
                file.write_text(json.dumps(export))
                self.owner[run] = file
            zip_file.writestr("spatial-study-plan.json",json.dumps({
                "schemaVersion":1,"mode":"development-only","studySeed":m218.SOURCE_SEED,
                "humanTruth":"ALL_UNKNOWN","runIds":list(m218.RUNS),"radiusPixels":26,
                "runs":study}))
        (self.site/"index.html").write_text("<ul><li>Original nine pages</li></ul>")
        (self.site/"published-development-manifest.json").write_text(json.dumps({
            "m215StudySeed":m218.SOURCE_SEED,
            "routes":["annoteren/"+name+"/" for name in
                      ["run-02","dev-a","dev-b","dev-c","dev-d","dev-e",*m218.RUNS]]}))

    def close(self):self.t.cleanup()


class M218Tests(unittest.TestCase):
    def setUp(self):self.fixture=Fixture()
    def tearDown(self):self.fixture.close()

    def test_blank_study_preserves_source_files_and_declares_new_routes(self):
        f=self.fixture
        source_before=f.archive.read_bytes()
        old={(p.relative_to(f.site).as_posix()):p.read_bytes()
             for p in f.site.rglob("*") if p.is_file()}
        m218.public_build(f.archive,f.site,"")
        assert source_before==f.archive.read_bytes()
        for p,v in old.items():
            if p not in ("index.html","published-development-manifest.json"):
                self.assertEqual(v,(f.site/p).read_bytes())
        page=(f.site/"annoteren/dev-f-points/index.html").read_text()
        self.assertIn("viewBox=\"0 0 390 844\"",page)
        self.assertIn("preserveAspectRatio=\"xMidYMid meet\"",page)
        self.assertNotIn("/*ARGUS_POINT_STUDY*/",page)
        self.assertIn('"sourceSeed":"'+m218.SOURCE_SEED+'"',page)
        self.assertNotIn('"state":"visible"',page)
        self.assertNotIn("run-01",page)
        self.assertNotIn("fetch(",page)
        self.assertEqual(0,len(list(f.site.glob("annoteren/*-points/frame-*.png"))))
        self.assertEqual(3,len(list(f.site.glob("annoteren/*-points/index.html"))))

    def test_private_export_creates_pixel_samples_without_learning(self):
        f=self.fixture
        before={run:path.read_bytes() for run,path in f.owner.items()}
        report=m218.validate_private(f.archive,list(f.owner.values()),"")
        self.assertEqual("STRUCTURALLY_READY",report["status"])
        self.assertEqual(set(report["runs"]),set(m218.RUNS))
        for run,group in report["runs"].items():
            self.assertEqual(3,group["frames"]["visible"])
            self.assertEqual(3,group["frames"]["absent"])
            self.assertEqual(6,group["frames"]["unknown"])
            self.assertGreaterEqual(group["offCenterVisible"],2)
            self.assertEqual(3,sum(row["label"]=="present" for row in group["derivedRows"]))
            self.assertTrue(all(row["source"] in ("owner-click","single-player-spatial-exclusion",
                                                 "owner-confirmed-no-player") for row in group["derivedRows"]))
            self.assertFalse(any(row["step"]>=6 for row in group["derivedRows"]))
        self.assertEqual(before,{run:path.read_bytes() for run,path in f.owner.items()})
        self.assertNotIn("weights",report.keys())

    def test_private_insufficient_offcenter_support_explicit_not_ready(self):
        f=self.fixture
        path=f.owner["dev-f"]
        data=json.loads(path.read_text())
        for frame in data["frames"][:3]:
            frame["player"]["center"]={"x":195,"y":422}
        path.write_text(json.dumps(data))
        report=m218.validate_private(f.archive,list(f.owner.values()),"")
        self.assertEqual("STUDY_NOT_READY",report["status"])
        self.assertEqual(0,report["runs"]["dev-f"]["offCenterVisible"])

    def test_tampered_archive_location_and_heldout_export_fail_closed(self):
        f=self.fixture
        with self.assertRaisesRegex(ValueError,"SHA256"):
            m218.original_images(f.archive,"f"*64)
        path=f.owner["dev-g"]
        data=json.loads(path.read_text())
        data["runId"]="run-01"
        path.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError,"held-out"):
            m218.validate_private(f.archive,list(f.owner.values()),"")
        data["runId"]="dev-g"
        data["frames"][0]["player"]["center"]["x"]=400
        path.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError,"in-bounds"):
            m218.validate_private(f.archive,list(f.owner.values()),"")
        data["frames"][0]["player"]["center"]["x"]=120
        data["frames"][0]["screenshotSha256"]="tampered"
        path.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError,"hash"):
            m218.validate_private(f.archive,list(f.owner.values()),"")


if __name__=="__main__":
    unittest.main()
