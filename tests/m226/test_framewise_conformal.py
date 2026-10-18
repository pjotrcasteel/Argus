"""M2.26 tests use invented pixels and fictional owner states ONLY.

No original WyrmForge images, private human JSON, trained weights or O/P/Q data.
"""
import hashlib
import sys
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from tools.m226 import framewise_conformal as m226


def fake(run, step, state, x=190, identical=False):
    rgb = np.full((844, 390, 3), (step*7+33) % 240, dtype=np.uint8)
    if state == "visible":
        rgb[398:431, max(0,x-10):min(390,x+10)] = [222, 125, 43]
    digest = hashlib.sha256(rgb.tobytes()).hexdigest() if identical else hashlib.sha256(
        (run + ":" + str(step)).encode()).hexdigest()
    return {"run": run, "step": step, "state": state,
            "sha": digest, "center": {"x": x, "y": 421} if state == "visible" else None,
            "rgb": rgb}


def data():
    return {run: [fake(run,j,"visible",x=40+j*45) for j in range(4)] +
            [fake(run,j+4,"absent") for j in range(6)]
            for run in m226.RUNS}


class FrozenFramewiseTests(unittest.TestCase):
    def test_fit_and_calibration_are_sha_disjoint_whole_absent_only(self):
        by_run=data()
        fit, cal=m226.split_fit_calibration(by_run, ["dev-l","dev-m"])
        self.assertEqual(len(cal),6)
        self.assertEqual(len([f for f in fit if f["state"]=="absent"]),6)
        self.assertEqual(len([f for f in fit if f["state"]=="visible"]),8)
        self.assertEqual(len({f["sha"] for f in fit}&{f["sha"] for f in cal}),0)
        self.assertTrue(all(f["state"]=="absent" for f in cal))
        self.assertEqual([f["step"] for f in cal if f["run"]=="dev-l"],[4,6,8])
        self.assertEqual([f["step"] for f in cal if f["run"]=="dev-m"],[4,6,8])

    def test_duplicate_png_bytes_are_not_independent_calibration_evidence(self):
        by_run=data()
        by_run["dev-m"][5]["sha"] = by_run["dev-m"][4]["sha"]
        fit, cal=m226.split_fit_calibration(by_run, ["dev-l","dev-m"])
        self.assertEqual(len({f["sha"] for f in cal}),len(cal))
        self.assertFalse({f["sha"] for f in cal}&{f["sha"] for f in fit})
        self.assertEqual(6,len(cal))
        self.assertEqual(5,len([f for f in fit if f["state"]=="absent"]))

    def test_absent_insufficient_rejects_and_heldout_runs_not_accepted_as_fit(self):
        subset=data()
        subset["dev-m"]=[f for f in subset["dev-m"] if f["state"]=="visible"]
        with self.assertRaisesRegex(ValueError,"NOT_READY"):
            m226.split_fit_calibration(subset,["dev-l","dev-m"])
        with self.assertRaisesRegex(ValueError,"Only independent development"):
            m226.split_fit_calibration(data(),["dev-l","run-01"])
        with self.assertRaisesRegex(ValueError,"Only independent development"):
            m226.split_fit_calibration(data(),["dev-l"])

    def test_rank_uses_out_of_fit_absent_whole_frames_and_tie_conservatism(self):
        self.assertAlmostEqual(m226.rank_tail_probability(10.,[1,2,3,4,5,6]),1/7)
        self.assertAlmostEqual(m226.rank_tail_probability(4.,[1,2,3,4,5,6]),4/7)
        self.assertEqual(m226.rank_tail_probability(0.,[0,0,0,0]),1.0)
        with self.assertRaisesRegex(ValueError,"NOT_READY"):
            m226.rank_tail_probability(1.,[.2,.4,.9])
        self.assertEqual(m226.ALPHA,.20)
        self.assertEqual(m226.BACKGROUND_QUANTILE,.95)

    def test_unclipped_full_frame_candidate_statistic_never_saturates_to_sigmoid_one(self):
        rgb=np.zeros((844,390,3),dtype=np.uint8)
        rgb[406:434,170:210]=[230,65,25]
        f={"rgb":rgb,"center":None}
        rng=np.random.default_rng(246)
        w=rng.normal(size=246)
        model=SimpleNamespace(mean=np.zeros(246),std=np.ones(246),
                              weights=w,bias=1000.)  # extreme logits, no sigmoid clipping
        confidence, position=m226.image_evidence(
            f,model,grid=((190,420),(14,20),(350,790),(10,800)))
        self.assertTrue(np.isfinite(confidence))
        self.assertGreaterEqual(confidence,0.)
        self.assertIn(position,((190,420),(14,20),(350,790),(10,800)))
        # The huge common bias cancels from contrast; it cannot force p=1.
        model2=SimpleNamespace(mean=model.mean,std=model.std,weights=w,bias=0.)
        new_confidence,_=m226.image_evidence(f,model2,grid=((190,420),(14,20),(350,790),(10,800)))
        self.assertAlmostEqual(confidence,new_confidence,places=7)

    def test_prediction_does_not_leak_truth_and_predeclared_gate(self):
        f=fake("dev-l",1,"visible")
        fake_model=SimpleNamespace()
        with patch.object(m226,"image_evidence",return_value=(10.,(30,420))):
            pred=m226.predict(f,fake_model,[1,2,3,4,5,6])
        self.assertTrue(pred["present"])
        self.assertEqual(pred["center"],(30,420))
        with patch.object(m226,"image_evidence",return_value=(1.,(30,420))):
            declined=m226.predict(f,fake_model,[1,2,3,4,5,6])
        self.assertFalse(declined["present"])
        self.assertIsNone(declined["center"])
        self.assertEqual(m226.SUCCESS_MIN_VISIBLE_LOCATION_RECALL,.50)
        self.assertEqual(m226.SUCCESS_MAX_ABSENT_FALSE_POSITIVE_RATE,.25)

    def test_original_prospective_run_rejected_without_reading_pixels(self):
        with self.assertRaisesRegex(ValueError,"Only three original"):
            m226.evaluate({"dev-l":[],"dev-m":[],"dev-o":[]},{},0)
        with self.assertRaisesRegex(ValueError,"Only three original"):
            m226.evaluate({"dev-l":[],"dev-m":[],"run-01":[]},{},0)


if __name__=="__main__":
    unittest.main()
