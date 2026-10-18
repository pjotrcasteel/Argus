"""M2.22 entirely artificial pixel/label test fixtures; NEVER real owner data."""
import copy
import hashlib
import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path

import numpy as np

SRC = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(SRC))
from tools.m222 import offline_localization as m222


class SyntheticTests(unittest.TestCase):
    def synthetic_runs(self):
        data = {}
        for run_id, run in enumerate(m222.RUNS):
            rows = []
            for step in range(6):
                rgb = np.zeros((844, 390, 3), dtype=np.uint8)
                rgb[:, :, 0] = 28 + run_id * 22
                rgb[:, :, 1] = 30 + step * 3
                visible = step < 3
                center = {"x": (22, 185, 365)[step], "y": 418} if visible else None
                if center is not None:
                    left, right = max(0, center["x"] - 10), min(390, center["x"] + 10)
                    rgb[408:429, left:right] = [210, 95, 50]
                # At most one *actual* same source across different development
                # groups; must be excluded from training whenever held out.
                if step == 4 and run in ("dev-i", "dev-k"):
                    rgb[:, :] = 0
                checksum = hashlib.sha256(rgb.tobytes()).hexdigest()
                rows.append({"run": run, "step": step, "sha": checksum,
                             "state": "visible" if visible else "absent",
                             "center": center, "rgb": rgb})
            data[run] = rows
        return data

    def test_appearance_has_only_five_finite_image_features(self):
        rgb = np.full((844,390,3), (65,74,83), dtype=np.uint8)
        pad = np.pad(rgb,((36,36),(36,36),(0,0)),mode="edge")
        for x,y in ((0,0),(195,422),(389,843)):
            v=m222.features(pad,x,y)
            self.assertEqual(v.shape,(5,))
            self.assertTrue(np.isfinite(v).all())
        self.assertEqual(m222.FEATURE_NAMES,
                         ["mean_luminance","luminance_std","ring_contrast",
                          "gradient_density","mean_saturation"])

    def test_learning_blocks_original_shared_png_across_whole_run_folds(self):
        inputs=self.synthetic_runs()
        report=m222.evaluate(inputs,{"syntheticOnly":True})
        self.assertEqual(report["status"],"EXPLORATORY_OFFLINE_TRAINED")
        self.assertEqual([r["heldOutRun"] for r in report["folds"]],list(m222.RUNS))
        for f in report["folds"]:
            self.assertEqual(len(f["fitRuns"]),2)
            self.assertNotIn(f["heldOutRun"],f["fitRuns"])
            self.assertEqual(f["imageModel"]["visibleFrames"],3)
            self.assertEqual(f["centerAlwaysPresentBaseline"]["falsePositive"],3)
        by_run={f["heldOutRun"]:f for f in report["folds"]}
        self.assertGreaterEqual(by_run["dev-i"]["sharedOriginalPNGExcluded"],1)
        self.assertGreaterEqual(by_run["dev-k"]["sharedOriginalPNGExcluded"],1)
        self.assertFalse(any("dev-" in name for name in m222.FEATURE_NAMES))
        self.assertEqual(len(report["privateResearchModel"]["weights"]),5)

    def test_unknown_cannot_be_silently_converted_into_absent(self):
        bad=[{"state":"unknown","center":None}]
        with self.assertRaisesRegex(ValueError,"UNKNOWN"):
            m222.score(bad,[{"present":False,"center":None}])

    def test_fake_or_heldout_research_source_is_rejected(self):
        with tempfile.TemporaryDirectory() as root:
            file=Path(root)/"source.zip"
            file.write_bytes(b"Not frozen PNG source or ZIP")
            with self.assertRaisesRegex(ValueError,"original.*ZIP changed"):
                m222.read_source_and_private(file,[])
            for run in ("run-01","dev-x"):
                bad={run:[{"state":"absent"}]}
                with self.assertRaisesRegex(ValueError,"Only frozen"):
                    m222.evaluate(bad,{})


if __name__=="__main__":
    unittest.main()
