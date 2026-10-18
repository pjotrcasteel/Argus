"""Entirely ARTIFICIAL 390x844 RGB PNG + fictional human points. No owner data."""
import hashlib
import io
import json
import tempfile
import unittest
from pathlib import Path
from zipfile import ZipFile

import numpy as np
from PIL import Image

from tools.m234 import evaluate_private_uvw as audit


def original_png(i):
    image = Image.new("RGB", (390, 844), (i % 255, i // 255, 31))
    bio = io.BytesIO()
    image.save(bio, format="PNG")
    return bio.getvalue()


class PrivateOriginalUVWAuditTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.archive = self.root/"source.zip"
        self.labels = self.root/"reviews"
        self.labels.mkdir()
        self.members = {"index.html": b"only fictional locally generated images"}
        self.runs = []
        for run_index, run in enumerate(audit.RUNS):
            source, prior, records = [], [], []
            for i in range(18):
                step = 1 + 3*i
                image = original_png(10 + 50*run_index + i)
                predecessor = original_png(200 + 50*run_index + i)
                image_sha = hashlib.sha256(image).hexdigest()
                old_sha = hashlib.sha256(predecessor).hexdigest()
                shot = f"frame-{step:04d}.png"
                old = f"context/{run}/prev-for-{step:04d}.png"
                self.members[f"annoteren/{run}/{shot}"] = image
                self.members[old] = predecessor
                source.append({"step":step, "file":shot, "sha256":image_sha})
                prior.append({"targetStep":step, "priorStep":step-1, "file":old, "sha256":old_sha})
                state = "absent" if i < 5 else "visible"
                player = {"state":state, "center":None if state=="absent" else {"x":0 if i%2 else 389, "y":420}}
                records.append({"step":step, "screenshotSha256":image_sha, "player":player})
            self.runs.append({"runId":run, "images":source, "immediatePriorOriginalFrames":prior})
            self.members[f"annoteren/{run}/index.html"] = b"<html>fictional ALL UNKNOWN</html>"
            (self.labels/f"m232-{run}-player-points.json").write_text(json.dumps({
                "schemaVersion":1,"studySeed":audit.STUDY_SEED,
                "sourceStudySeed":audit.STUDY_SEED,"runId":run,
                "partition":"development","frames":records}))
        self.members["study-manifest.json"] = json.dumps({
            "schemaVersion":1,"seed":audit.STUDY_SEED,"sourceRole":"independent-development-source-only",
            "runIds":list(audit.RUNS),"targetsPerRun":18,"originalTargetImages":54,
            "originalPrecedingContextImages":54,"humanGroundTruth":"ALL_UNKNOWN",
            "prospectiveOriginalsAccessed":False,"runs":self.runs}).encode()
        self.rezip()

    def rezip(self):
        with ZipFile(self.archive, "w") as archive:
            for name, data in self.members.items():
                archive.writestr(name, data)
        self.sha = hashlib.sha256(self.archive.read_bytes()).hexdigest()

    def admit(self):
        return audit.admit_original_reviews(self.archive, self.labels, expected_archive_sha=self.sha)

    def test_admit_exact_54_original_targets_and_private_labels(self):
        samples, meta = self.admit()
        self.assertEqual(54,meta["uniqueImageSha256"])
        self.assertEqual(54,sum(len(f) for f in samples.values()))
        self.assertTrue(all(meta["runStateCounts"][r]=={"visible":13,"absent":5,"unknown":0}
                            for r in audit.RUNS))
        self.assertEqual((844,390,3),samples["dev-u"][0]["rgb"].shape)

    def test_wrong_original_sha_fails_closed(self):
        self.members["annoteren/dev-u/frame-0001.png"] = original_png(777)
        self.rezip()
        with self.assertRaisesRegex(ValueError,"Original source screenshot"):
            self.admit()

    def test_wrong_owner_source_sha_or_step_fails_closed(self):
        path = self.labels/"m232-dev-v-player-points.json"
        record = json.loads(path.read_text())
        record["frames"][2]["screenshotSha256"] = "0"*64
        path.write_text(json.dumps(record))
        with self.assertRaisesRegex(ValueError,"source chronology|wrong original step"):
            self.admit()

    def test_visible_outside_original_resolution_fails(self):
        path = self.labels/"m232-dev-w-player-points.json"
        record=json.loads(path.read_text())
        record["frames"][6]["player"]["center"]["x"]=390
        path.write_text(json.dumps(record))
        with self.assertRaisesRegex(ValueError,"in-bounds"):
            self.admit()

    def test_absent_cannot_carry_hallucinated_point(self):
        path = self.labels/"m232-dev-w-player-points.json"
        record=json.loads(path.read_text())
        record["frames"][0]["player"]["center"]={"x":20,"y":420}
        path.write_text(json.dumps(record))
        with self.assertRaisesRegex(ValueError,"cannot fabricate"):
            self.admit()

    def test_additional_private_source_or_prospective_member_forbidden(self):
        self.members["annoteren/dev-o/frame-0001.png"] = b"no"
        self.rezip()
        with self.assertRaisesRegex(ValueError,"Forbidden source/archive member"):
            self.admit()

    def test_unknown_is_retained_and_not_imputed(self):
        path = self.labels/"m232-dev-u-player-points.json"
        record=json.loads(path.read_text())
        record["frames"][4]["player"]={"state":"unknown","center":None}
        path.write_text(json.dumps(record))
        _, meta=self.admit()
        self.assertEqual(1, meta["runStateCounts"]["dev-u"]["unknown"])
        self.assertEqual(4, meta["runStateCounts"]["dev-u"]["absent"])

    def test_exact_duplicate_png_cannot_have_conflicting_human_truth(self):
        a = self.members["annoteren/dev-u/frame-0001.png"]
        other = self.runs[1]["images"][0]
        path = f"annoteren/dev-v/{other['file']}"
        self.members[path] = a
        other["sha256"] = self.runs[0]["images"][0]["sha256"]
        data = json.loads((self.labels/"m232-dev-v-player-points.json").read_text())
        data["frames"][0]["screenshotSha256"] = other["sha256"]
        data["frames"][0]["player"]={"state":"visible","center":{"x":123,"y":420}}
        (self.labels/"m232-dev-v-player-points.json").write_text(json.dumps(data))
        self.members["study-manifest.json"] = json.dumps({
            "schemaVersion":1,"seed":audit.STUDY_SEED,"sourceRole":"independent-development-source-only",
            "runIds":list(audit.RUNS),"targetsPerRun":18,"originalTargetImages":54,
            "originalPrecedingContextImages":54,"humanGroundTruth":"ALL_UNKNOWN",
            "prospectiveOriginalsAccessed":False,"runs":self.runs}).encode()
        self.rezip()
        with self.assertRaisesRegex(ValueError,"Contradictory human truth"):
            self.admit()

    def test_frozen_trial_on_synthetic_constant_ranker_is_no_go(self):
        samples,_=self.admit()
        features=288
        def constant_fit(_fit):
            return {"mean":np.zeros(features), "std":np.ones(features),
                    "weights":np.zeros(features), "bias":0.0}
        result=audit.evaluate_frozen(samples,grid=((0,420),(195,420),(389,420)),
                                     model_fit=constant_fit)
        self.assertFalse(result["allInvestigateFurther"])
        self.assertEqual(3,len(result["wholeRunOutFolds"]))
        self.assertTrue(all(r["metrics"]["status"]=="NO_GO" for r in result["wholeRunOutFolds"]))
        self.assertTrue(all(r["calibrationDistinctTrueAbsent"]>=4 for r in result["wholeRunOutFolds"]))
        self.assertTrue(all(r["metrics"]["correctLocationWithin35"]==0 for r in result["wholeRunOutFolds"]))
        self.assertFalse(result["prospectiveOriginalsOpened"])
        self.assertFalse(result["liveAgentChanged"])


if __name__ == "__main__":
    unittest.main()
