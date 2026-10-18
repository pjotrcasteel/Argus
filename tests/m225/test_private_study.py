"""Only artificial traces, 390x844 RGB PNGs and fictional owner references."""
import hashlib
import json
import struct
import zlib
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tests/m223"))
from test_new_cohorts import CohortTests, valid_png
from tools.m223.freeze_new_cohorts import original_frames
from tools.m225 import private_source_study as study


class PrivateAdmissionTests(unittest.TestCase):
    def setUp(self):
        self.fixture = CohortTests()
        self.fixture.setUp()
        raw_png = valid_png()
        for sample_run in ("dev-l", "dev-m", "dev-n"):
            for sample_step in range(40):
                identity = (sample_run + "-" + str(sample_step)).encode()
                tag = struct.pack(">I", len(identity)) + b"tEXt" + identity
                tag += struct.pack(">I", zlib.crc32(b"tEXt" + identity) & 0xffffffff)
                sample_path = self.fixture.inputs / sample_run / f"frame-{sample_step:04d}.png"
                sample_path.write_bytes(raw_png[:33] + tag + raw_png[33:])
        self.original = self.fixture.root / "fictional-development"
        self.plan = original_frames(self.fixture.inputs, "development", self.original)
        self.archive = self.fixture.root / "fictional-development.zip"
        with zipfile.ZipFile(self.archive, "w") as z:
            for p in sorted(self.original.rglob("*")):
                if p.is_file():
                    z.write(p, p.relative_to(self.original).as_posix())
        self.expected_sha = study.SOURCE_ARCHIVE_SHA256
        study.SOURCE_ARCHIVE_SHA256 = hashlib.sha256(self.archive.read_bytes()).hexdigest()
        self.exports = []
        for run in self.plan["runs"]:
            result = {"schemaVersion": 1, "studySeed": study.SOURCE_SEED,
                      "sourceStudySeed": study.SOURCE_SEED, "runId": run["runId"],
                      "partition": "development", "frames": []}
            for i, original in enumerate(run["images"]):
                # Fictional labels; 12 visible, 6 absent, 4 offcenter.
                player = ({"state": "visible", "center": {"x": 55 if i < 4 else 194, "y": 423}}
                          if i < 12 else {"state": "absent", "center": None})
                result["frames"].append({"step": original["step"],
                                         "screenshotSha256": original["sha256"],
                                         "player": player})
            path = self.fixture.root / (run["runId"] + "-synthetic-owner.json")
            path.write_text(json.dumps(result))
            self.exports.append(path)

    def tearDown(self):
        study.SOURCE_ARCHIVE_SHA256 = self.expected_sha
        self.fixture.tearDown()

    def test_admit_all_three_exact_original_source_frames_without_private_upload(self):
        originals, coverage, unique = study.validate_and_load(self.archive, self.exports)
        self.assertEqual(list(originals), list(study.DEVELOPMENT))
        self.assertEqual(54, sum(len(x) for x in originals.values()))
        self.assertEqual(54, unique)  # Same invented static PNG, never independent evidence.
        for run in study.DEVELOPMENT:
            self.assertEqual(12, coverage[run]["visible"])
            self.assertEqual(6, coverage[run]["absent"])
            self.assertEqual(4, coverage[run]["offCenterVisible"])
            self.assertEqual(40, coverage[run]["sourceFrames"])

    def test_refuse_tampered_sha_or_unknown_owner_state(self):
        label = json.loads(self.exports[0].read_text())
        label["frames"][0]["screenshotSha256"] = "f" * 64
        self.exports[0].write_text(json.dumps(label))
        with self.assertRaisesRegex(ValueError, "frozen original"):
            study.validate_and_load(self.archive, self.exports)
        label["frames"][0]["screenshotSha256"] = self.plan["runs"][0]["images"][0]["sha256"]
        label["frames"][0]["player"] = {"state": "unknown", "center": None}
        label["frames"][12]["player"] = {"state": "unknown", "center": None}
        label["frames"][13]["player"] = {"state": "unknown", "center": None}
        label["frames"][14]["player"] = {"state": "unknown", "center": None}
        label["frames"][15]["player"] = {"state": "unknown", "center": None}
        self.exports[0].write_text(json.dumps(label))
        with self.assertRaisesRegex(ValueError, "NOT_READY"):
            study.validate_and_load(self.archive, self.exports)

    def test_refuse_owner_outside_dimensions_and_original_heldout(self):
        label = json.loads(self.exports[0].read_text())
        label["frames"][0]["player"] = {"state": "visible", "center": {"x": 390, "y": 844}}
        self.exports[0].write_text(json.dumps(label))
        with self.assertRaisesRegex(ValueError, "outside source screenshot"):
            study.validate_and_load(self.archive, self.exports)
        label["frames"][0]["player"] = {"state": "visible", "center": {"x": 55, "y": 423}}
        label["runId"] = "run-01"
        self.exports[0].write_text(json.dumps(label))
        with self.assertRaisesRegex(ValueError, "owner export"):
            study.validate_and_load(self.archive, self.exports)

    def test_refuse_unapproved_original_zip(self):
        study.SOURCE_ARCHIVE_SHA256 = "0" * 64
        with self.assertRaisesRegex(ValueError, "ZIP SHA256 differs"):
            study.validate_and_load(self.archive, self.exports)

    def test_source_archive_contains_no_real_wyrmforge_data_or_prospective(self):
        with zipfile.ZipFile(self.archive) as original:
            names=original.namelist()
        self.assertFalse(any("dev-o" in n or "dev-p" in n or "dev-q" in n for n in names))
        self.assertFalse(any("run-01" in n for n in names))


if __name__ == "__main__":
    unittest.main()
