"""Hard failure gates for M2.15 blind local candidate protocol (synthetic only)."""
import importlib.util
import json
import struct
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

PATH = Path(__file__).with_name("spatial_candidate_review.py")
SPEC = importlib.util.spec_from_file_location("m215_spatial_review", PATH)
module = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = module
SPEC.loader.exec_module(module)


def fake_archive(filename: Path, malformed=None):
    png = module.PNG_HEADER + struct.pack(">IIBBBBB", 390, 844, 8, 2, 0, 0, 0)
    payload = {"schemaVersion": 1, "mode": "development-only",
               "seed": module.SOURCE_SEED, "requestedFramesPerRun": 24,
               "assignments": []}
    with zipfile.ZipFile(filename, "w", compression=zipfile.ZIP_DEFLATED) as z:
        for run in module.RUNS:
            steps = list(range(24))
            payload["assignments"].append(
                {"runId": run, "partition": "development", "frameSteps": steps})
            rows = []
            for step in range(24):
                name = f"frame-{step:04d}.png"
                rows.append({"step": step, "screenshot": name,
                             "evidence": {"phase": "exploring" if step < 18 else "transition",
                                          "reason": f"SYNTHETIC fixture-{run}"}})
                z.writestr(f"{run}/{name}", png + (run + str(step)).encode())
            z.writestr(f"{run}/trace.jsonl", "\n".join(json.dumps(row) for row in rows) + "\n")
        if malformed == "held-out":
            payload["assignments"][1]["partition"] = "held-out"
        if malformed == "old-seed":
            payload["seed"] = "M2.4 HELD OUT DO NOT READ"
        if malformed == "dup-steps":
            payload["assignments"][0]["frameSteps"][0] = 1
        z.writestr("study-plan.json", json.dumps(payload))
        # Poison is never a legal target for this reader and never used.
        z.writestr("run-01/trace.jsonl", "ABORT_IF_EXAMINED")
    return filename


class CandidateReferenceTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="argus-m215-")
        self.root = Path(self.tmp.name)
        self.archive = fake_archive(self.root / "realistic-fake.zip")

    def tearDown(self):
        self.tmp.cleanup()

    def test_frozen_full_study_36_original_images_72_unknown_local_probes(self):
        dest = self.root / "site"
        plan = module.build(self.archive, dest)
        self.assertEqual("development-only", plan["mode"])
        self.assertEqual(module.SEED, plan["studySeed"])
        self.assertEqual(3, len(plan["runs"]))
        self.assertEqual(["dev-f", "dev-g", "dev-h"], plan["runIds"])
        self.assertTrue((dest / "index.html").exists())
        for group in plan["runs"]:
            run = group["runId"]
            self.assertEqual(12, len(group["images"]))
            self.assertEqual(24, len(group["candidates"]))
            self.assertEqual(len(set(x["step"] for x in group["candidates"])), 12)
            self.assertEqual(12, sum(x["role"] == "center-anchor" for x in group["candidates"]))
            self.assertEqual(12, sum(x["role"] == "spatial-control" for x in group["candidates"]))
            self.assertTrue(all(x["label"] == "unknown" and x["radius"] == 26
                                for x in group["candidates"]))
            self.assertEqual(12, len(list((dest / "annoteren" / run).glob("frame-*.png"))))
            html = (dest / "annoteren" / run / "index.html").read_text()
            self.assertNotIn("ARGUS_STUDY*/", html)
            self.assertNotIn("threatLocations", html)
            self.assertNotIn("playerX", html)
            self.assertNotIn("ABORT_IF_EXAMINED", html)
            self.assertIn("const study={", html)

    def test_repeatability_no_models_or_annotations_in_frame_choice(self):
        first = module.build(self.archive, self.root / "site-one")
        again = module.build(self.archive, self.root / "site-two")
        self.assertEqual(first, again)
        self.assertEqual(
            (self.root / "site-one" / "spatial-study-plan.json").read_bytes(),
            (self.root / "site-two" / "spatial-study-plan.json").read_bytes())
        for run in module.RUNS:
            a = (self.root / "site-one" / "annoteren" / run / "index.html").read_bytes()
            b = (self.root / "site-two" / "annoteren" / run / "index.html").read_bytes()
            self.assertEqual(a, b)

    def test_validation_never_converts_unknown_to_absent_or_ready(self):
        dest = self.root / "site"
        plan = module.build(self.archive, dest)
        group = plan["runs"][0]
        export = {"schemaVersion": 1, "studySeed": module.SEED,
                  "runId": group["runId"], "probes": group["candidates"]}
        file = self.root / "labels.json"
        file.write_text(json.dumps(export))
        report = module.validate_export(dest / "spatial-study-plan.json", file)
        self.assertEqual(24, report["labels"]["unknown"])
        self.assertEqual(0, report["humanReviewed"])
        self.assertFalse(report["trainingReady"])
        export["probes"][0]["label"] = "present"
        export["probes"][1]["label"] = "absent"
        file.write_text(json.dumps(export))
        counts = module.validate_export(dest / "spatial-study-plan.json", file)
        self.assertEqual(1, counts["labels"]["present"])
        self.assertEqual(1, counts["labels"]["absent"])
        self.assertEqual(22, counts["labels"]["unknown"])
        self.assertFalse(counts["trainingReady"])

    def test_validation_rejects_fake_moved_duplicate_or_absent_fields(self):
        dest = self.root / "site"
        plan = module.build(self.archive, dest)
        original = plan["runs"][1]["candidates"]
        export = {"schemaVersion": 1, "studySeed": module.SEED, "runId": "dev-g",
                  "probes": [dict(x) for x in original]}
        file = self.root / "labels.json"
        for alter in ("moved", "duplicate", "extra", "incorrect", "missing"):
            data = json.loads(json.dumps(export))
            if alter == "moved": data["probes"][0]["x"] += 1
            elif alter == "duplicate": data["probes"][1] = data["probes"][0]
            elif alter == "extra": data["probes"][0]["predictedPlayerX"] = 195
            elif alter == "incorrect": data["probes"][0]["label"] = "absant"
            else: data["probes"].pop()
            file.write_text(json.dumps(data))
            with self.subTest(alter=alter):
                with self.assertRaises(ValueError):
                    module.validate_export(dest / "spatial-study-plan.json", file)

    def test_heldout_old_seed_duplicates_and_overwrite_fail_closed(self):
        for problem in ("held-out", "old-seed", "dup-steps"):
            archive = fake_archive(self.root / (problem + ".zip"), malformed=problem)
            with self.subTest(problem=problem):
                with self.assertRaises(ValueError):
                    module.build(archive, self.root / ("site-" + problem))
        site = self.root / "existing"
        module.build(self.archive, site)
        with self.assertRaises(ValueError):
            module.build(self.archive, site)


if __name__ == "__main__":
    unittest.main()
