"""Artificial-only M2.17 privacy, provenance, contradiction and adjudication tests."""
import hashlib
import importlib.util
import json
import struct
import tempfile
import unittest
import zlib
from pathlib import Path
from zipfile import ZipFile

SCRIPT = Path(__file__).resolve().parents[2] / "tools/m217/reference_audit.py"
spec = importlib.util.spec_from_file_location("m217_reference_audit", SCRIPT)
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class Fixture:
    def __init__(self):
        self.temp = tempfile.TemporaryDirectory(prefix="argus-m217-synthetic-")
        self.folder = Path(self.temp.name)
        self.zip = self.folder / "original.zip"
        self.labels = {}
        self.pairs = {}
        plan_runs = []
        with ZipFile(self.zip, "w") as z:
            z.writestr("index.html", "<html>SYNTHETIC</html>")
            for run_id in audit.RUNS:
                images, probes, labels = [], [], []
                z.writestr(f"annoteren/{run_id}/index.html", "<html>SYNTHETIC</html>")
                for step in range(12):
                    def chunk(tag, payload):
                        return (struct.pack(">I", len(payload)) + tag + payload +
                                struct.pack(">I", zlib.crc32(tag + payload) & 0xffffffff))
                    row = b"\x00" + bytes([35 + step, 67, 91]) * 390
                    png = (b"\x89PNG\r\n\x1a\n" +
                           chunk(b"IHDR", struct.pack(">IIBBBBB", 390, 844, 8, 2, 0, 0, 0)) +
                           chunk(b"IDAT", zlib.compress(row * 844, 3)) +
                           chunk(b"IEND", b""))
                    filename = f"frame-{step:04d}.png"
                    z.writestr(f"annoteren/{run_id}/{filename}", png)
                    images.append({"step": step, "file": filename,
                                   "sha256": hashlib.sha256(png).hexdigest()})
                    pair = []
                    for role, x, y in (("center-anchor", 195, 422),
                                       ("spatial-control", 72, 180)):
                        sample = f"{run_id}-step{step:04d}-{role}"
                        probe = {"sampleId": sample, "step": step, "x": x, "y": y,
                                 "radius": 26, "role": role, "label": "unknown"}
                        probes.append(probe)
                        label = dict(probe)
                        label["label"] = "present" if role == "center-anchor" and step < 3 else "absent"
                        labels.append(label)
                        pair.append(sample)
                    self.pairs[(run_id, step)] = pair
                plan_runs.append({"schemaVersion": 1, "runId": run_id,
                                  "partition": "development", "traceSha256": (
                                      {"dev-f": "a", "dev-g": "b", "dev-h": "c"}[run_id] * 64),
                                  "images": images, "candidateCount": 24, "candidates": probes})
                obj = {"schemaVersion": 1, "studySeed": audit.STUDY_SEED,
                       "runId": run_id, "probes": labels}
                path = self.folder / (run_id + ".json")
                path.write_text(json.dumps(obj))
                self.labels[run_id] = path
            z.writestr("spatial-study-plan.json", json.dumps({
                "schemaVersion": 1, "mode": "development-only",
                "sourceSeed": audit.SOURCE_SEED, "studySeed": audit.STUDY_SEED,
                "radiusPixels": 26, "humanTruth": "ALL_UNKNOWN",
                "runIds": list(audit.RUNS), "runs": plan_runs}))

    def inspect(self, expected=""):
        return audit.inspect(self.zip, [self.labels[k] for k in audit.RUNS], expected)

    def set(self, run, sample, key, value):
        path = self.labels[run]
        data = json.loads(path.read_text())
        next(p for p in data["probes"] if p["sampleId"] == sample)[key] = value
        path.write_text(json.dumps(data))

    def close(self):
        self.temp.cleanup()


class PrivateReferenceAdjudicationTests(unittest.TestCase):
    def setUp(self):
        self.data = Fixture()

    def tearDown(self):
        self.data.close()

    def test_consistent_study_never_fits_or_changes_owner_export(self):
        original = {key: file.read_bytes() for key, file in self.data.labels.items()}
        report, owner, images = self.data.inspect()
        self.assertEqual("REFERENCE_CONSISTENT", report["status"])
        self.assertEqual([], report["issues"])
        self.assertEqual(set(owner), set(audit.RUNS))
        self.assertEqual(len(images), 36)
        self.assertEqual(sum(v["present"] for v in report["counts"].values()), 9)
        self.assertFalse(set(report).intersection({"weights", "model", "accuracy", "trained"}))
        self.assertEqual(original, {key: file.read_bytes() for key, file in self.data.labels.items()})

    def test_disjoint_positive_requires_explicit_human_relabel(self):
        sample = self.data.pairs[("dev-f", 0)][1]
        self.data.set("dev-f", sample, "label", "present")
        original = self.data.labels["dev-f"].read_bytes()
        report, owner, images = self.data.inspect()
        self.assertEqual("REQUIRES_HUMAN_REVIEW", report["status"])
        self.assertEqual(1, len(report["issues"]))
        self.assertEqual("DISJOINT_DOUBLE_PRESENT", report["issues"][0]["reason"])
        self.assertEqual(["present", "present"], [x["label"] for x in report["issues"][0]["probes"]])
        html = audit.build_reviewer(report, owner, images)
        self.assertEqual(2, html.count('data-run="dev-f" data-sample='))
        self.assertIn("preserveAspectRatio=\"xMidYMid meet\"", html)
        self.assertIn("REQUIRES", report["status"])
        self.assertIn("link.download=\"m215-\"+run+\"-labels-rechecked.json\"", html)
        self.assertIn("document.getElementById(\"export\").disabled=conflicts.length>0", html)
        self.assertNotIn("fetch(", html)
        self.assertEqual(original, self.data.labels["dev-f"].read_bytes())

    def test_reject_wrong_source_archive_sha_and_corrupt_image(self):
        with self.assertRaisesRegex(ValueError, "SHA256"):
            self.data.inspect(expected="0"*64)
        with ZipFile(self.data.zip, "a") as z:
            # Duplicate member should be rejected, not silently accept first/last.
            z.writestr("annoteren/dev-f/frame-0000.png", b"EVIL")
        with self.assertRaisesRegex(ValueError, "archive contents"):
            self.data.inspect()

    def test_reject_moved_unsupported_duplicate_or_heldout_labels(self):
        sample = self.data.pairs[("dev-f", 0)][0]
        self.data.set("dev-f", sample, "x", 194)
        with self.assertRaisesRegex(ValueError, "frozen location"):
            self.data.inspect()
        self.data.set("dev-f", sample, "x", 195)
        self.data.set("dev-f", sample, "label", "inferred")
        with self.assertRaisesRegex(ValueError, "frozen location"):
            self.data.inspect()
        self.data.set("dev-f", sample, "label", "present")
        raw = json.loads(self.data.labels["dev-f"].read_text())
        raw["runId"] = "run-01"
        self.data.labels["dev-f"].write_text(json.dumps(raw))
        with self.assertRaisesRegex(ValueError, "owner export"):
            self.data.inspect()


if __name__ == "__main__":
    unittest.main()
