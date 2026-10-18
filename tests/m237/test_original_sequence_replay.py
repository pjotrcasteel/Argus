"""M2.37: synthetic-only PNG/source provenance and shadow replay tests.

These create temporary invented shapes, never WyrmForge originals or owner truth.
"""
import copy
import hashlib
import io
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import numpy as np
from PIL import Image

from tools.m235.causal_identity import Candidate
from tools.m237 import original_sequence_replay as m


class PrivateOriginalSequenceReplayTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.TemporaryDirectory()
        cls.root = Path(cls.tmp.name)
        cls.manifest = {
            "schemaVersion": 1, "studyType": "NEW_SOURCE",
            "gameVersion": "UNKNOWN_NOT_ATTESTED", "fit": [],
            "calibration": [], "runs": [],
        }
        for run in ("synthetic-fit-a", "synthetic-fit-b"):
            for step in range(6):
                cls.manifest["fit"].append(
                    cls.make_image(run, step, "absent" if step < 2 else "visible"))
        for run in ("synthetic-cal-a", "synthetic-cal-b"):
            for step in range(2):
                cls.manifest["calibration"].append(
                    cls.make_image(run, step, "absent"))
        for run in ("fresh-art-a", "fresh-art-b", "fresh-art-c"):
            frames = [cls.make_image(run, step, "visible", include_truth=False)
                      for step in range(4)]
            cls.manifest["runs"].append({"runId": run, "frames": frames})
        cls.grid = ((0,420), (80,420), (112,420), (195,420), (389,420))

    @classmethod
    def tearDownClass(cls):
        cls.tmp.cleanup()

    @classmethod
    def make_image(cls, run, step, state, include_truth=True):
        image = np.full((844,390,3), 25, np.uint8)
        image[60:84,10:34] = [20,180,220]  # static HUD decoy
        image[810:815,380:385] = [step*7+2, len(run), 11]  # unique PNG bytes
        if state == "visible":
            image[414:426,74:86] = [220,55,40]
            image[412:428,78:82] = [230,145,60]
        out = io.BytesIO()
        Image.fromarray(image).save(out, format="PNG")
        raw = out.getvalue()
        folder = cls.root / run
        folder.mkdir(exist_ok=True)
        path = folder / f"{step:03}.png"
        path.write_bytes(raw)
        record = {"run": run, "step": step,
                  "path": f"{run}/{step:03}.png",
                  "sha256": hashlib.sha256(raw).hexdigest()}
        if include_truth:
            record.update(state=state,
                          center={"x":80,"y":420} if state == "visible" else None)
        return record

    def test_full_private_originals_admitted_with_training_calibration_and_three_new_runs(self):
        fit, cal, eval_runs = m.admit_original_manifest(
            copy.deepcopy(self.manifest), self.root)
        self.assertEqual(12, len(fit))
        self.assertEqual(4, len(cal))
        self.assertEqual(3, len(eval_runs))
        self.assertEqual(4, len(eval_runs["fresh-art-a"]))
        self.assertTrue(all(r["center"] is None for r in cal))

    def test_m236_model_and_m235_tracker_run_on_actual_invented_png_bytes(self):
        report = m.replay_originals(copy.deepcopy(self.manifest), self.root, grid=self.grid)
        self.assertEqual("NOT_EVALUATED", report["status"])
        self.assertFalse(report["liveAgentChanged"])
        self.assertFalse(report["playerIdentityVerified"])
        self.assertEqual(64, len(report["frozenModelSha256"]))
        self.assertEqual("UNKNOWN_NOT_ATTESTED", report["gameVersion"])
        self.assertEqual(3, len(report["runs"]))
        self.assertTrue(all(all(f["playerPresent"] is None and
                                f["verifiedPlayerCenter"] is None
                                for f in rows) for rows in report["runs"].values()))

    def test_mismatched_bytes_duplicate_sources_and_label_leakage_fail_closed(self):
        modified = copy.deepcopy(self.manifest)
        modified["fit"][0]["sha256"] = "a"*64
        with self.assertRaisesRegex(ValueError, "source SHA"):
            m.admit_original_manifest(modified, self.root)
        modified = copy.deepcopy(self.manifest)
        modified["calibration"][0]["sha256"] = modified["fit"][0]["sha256"]
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            m.admit_original_manifest(modified, self.root)
        modified = copy.deepcopy(self.manifest)
        modified["runs"][0]["frames"][0]["center"] = {"x":80, "y":420}
        with self.assertRaisesRegex(ValueError, "must not expose"):
            m.admit_original_manifest(modified, self.root)

    def test_sealed_runs_and_seen_development_cannot_masquerade_as_new_cohort(self):
        modified = copy.deepcopy(self.manifest)
        modified["runs"][0]["runId"] = "dev-u"
        modified["runs"][0]["frames"] = [
            dict(frame, run="dev-u") for frame in modified["runs"][0]["frames"]]
        with self.assertRaisesRegex(ValueError, "genuinely new"):
            m.admit_original_manifest(modified, self.root)
        modified["runs"][0]["runId"] = "dev-o"
        modified["runs"][0]["frames"] = [
            dict(frame, run="dev-o") for frame in modified["runs"][0]["frames"]]
        with self.assertRaisesRegex(ValueError, "SEALED"):
            m.admit_original_manifest(modified, self.root)
        modified = copy.deepcopy(self.manifest)
        modified["calibration"][0]["run"] = "synthetic-fit-a"
        with self.assertRaisesRegex(ValueError, "disjoint"):
            m.admit_original_manifest(modified, self.root)

    def test_temporal_and_calibrated_appearance_must_agree_after_real_sequence(self):
        counter = [0]
        def sample(*args, **kwargs):
            step = counter[0] % 4
            counter[0] += 1
            return [Candidate(80,420,(1.,0.,.1),.9,.1 if step else 0.)]
        with (patch.object(m.contrast, "fit_metric", return_value={"test":True}),
              patch.object(m.contrast, "calibrate_absent", return_value=.1),
              patch.object(m.contrast, "model_digest", return_value="b"*64),
              patch.object(m.contrast, "source_candidates", side_effect=sample),
              patch.object(m.contrast, "classify_shadow", return_value={
                  "status":"UNVERIFIED_APPEARANCE_CANDIDATE",
                  "candidate":{"x":80,"y":420}, "playerPresent":None,
                  "verifiedPlayerCenter":None})):
            report = m.replay_originals(self.manifest, self.root, grid=self.grid)
        for frames in report["runs"].values():
            self.assertEqual(["ABSTAIN", "ABSTAIN", "ABSTAIN",
                              "SHADOW_ACCEPTED_HYPOTHESIS"],
                             [r["status"] for r in frames])
            self.assertEqual({"x":80, "y":420}, frames[-1]["candidate"])
            self.assertIsNone(frames[-1]["playerPresent"])

    def test_missing_original_predecessor_resets_temporal_evidence(self):
        source = copy.deepcopy(self.manifest)
        source["runs"][0]["frames"][-1]["step"] = 9
        counter = [0]
        def sample(*args, **kwargs):
            step = counter[0] % 4
            counter[0] += 1
            return [Candidate(80,420,(1.,0.,.1),.9,.1 if step else 0.)]
        with (patch.object(m.contrast, "fit_metric", return_value={"test":True}),
              patch.object(m.contrast, "calibrate_absent", return_value=.1),
              patch.object(m.contrast, "model_digest", return_value="b"*64),
              patch.object(m.contrast, "source_candidates", side_effect=sample),
              patch.object(m.contrast, "classify_shadow", return_value={
                  "status":"UNVERIFIED_APPEARANCE_CANDIDATE",
                  "candidate":{"x":80,"y":420}})):
            report = m.replay_originals(source, self.root, grid=self.grid)
        first = report["runs"]["fresh-art-a"]
        self.assertEqual("ABSTAIN", first[-1]["status"])
        self.assertFalse(first[-1]["consecutiveOriginalFrames"])

    def test_future_independent_human_labels_gate_per_run_not_smoke_success(self):
        predictions = {}
        reviews = []
        for run in ("fresh-art-a","fresh-art-b","fresh-art-c"):
            frames, labeled = [], []
            for step in range(11):
                sig = hashlib.sha256(f"{run}/{step}".encode()).hexdigest()
                candidate = {"x":80,"y":420} if step < 5 else None
                frames.append({"step":step, "sha256":sig,"candidate":candidate})
                labeled.append({"step":step, "sha256":sig,
                                "state":"visible" if step < 8 else "absent",
                                "center":{"x":80,"y":420} if step < 8 else None})
            predictions[run] = frames
            reviews.append({"runId":run,"frames":labeled})
        report = {"studyType":"NEW_SOURCE", "runs":predictions}
        outcome = m.evaluate_private_review(report, {"runs":reviews})
        self.assertEqual("PRELIMINARY_GO_RESEARCH_ONLY", outcome["status"])
        self.assertTrue(all(x["within35"] == 5 and x["falsePresence"] == 0
                            for x in outcome["runMetrics"].values()))
        reviews[0]["frames"][0]["sha256"] = "0"*64
        with self.assertRaisesRegex(ValueError,"frozen original"):
            m.evaluate_private_review(report, {"runs":reviews})

    def test_missing_review_and_human_unknown_never_becomes_absent(self):
        report = {"studyType":"NEW_SOURCE",
                  "runs":{"new-1":[{"step":0, "sha256":"a"*64, "candidate":None}]}}
        review = {"runs":[{"runId":"new-1", "frames":[
            {"step":0, "sha256":"a"*64, "state":"unknown", "center":None}]}]}
        result = m.evaluate_private_review(report,review)
        self.assertEqual("NOT_READY",result["status"])
        self.assertEqual(0,result["runMetrics"]["new-1"]["absent"])
        with self.assertRaisesRegex(ValueError,"Incomplete"):
            m.evaluate_private_review(report,{"runs":[]})


if __name__ == "__main__":
    unittest.main()
