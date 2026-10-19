"""M2.41 all-synthetic original-PNG admission, no real human labels on CI."""
import json
import shutil
import unittest
from pathlib import Path

from test_fresh_capture import FreshCaptureSyntheticTests
from tools.m238 import fresh_capture as original
from tools.m241.admit_blind_owner import audit


class FrozenBlindOwnerAdmissionTests(unittest.TestCase):
    def setUp(self):
        self.fixture = FreshCaptureSyntheticTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.tearDown)
        self.sources = self.fixture.captures
        self.review = self.fixture.root / "review"
        self.labels = self.fixture.root / "private-human-labels"
        self.labels.mkdir()
        original.freeze(self.sources, self.review)

    def export(self, run, *, visible=0, absent=0):
        study = json.loads((self.review / "study-manifest.json").read_text())
        rec = next(row for row in study["runs"] if row["runId"] == run)
        frames = []
        for i, img in enumerate(rec["images"]):
            state = "visible" if i < visible else "absent" if i < visible + absent else "unknown"
            center = {"x": 180 + i, "y": 420} if state == "visible" else None
            frames.append({"step": img["step"], "screenshotSha256": img["sha256"],
                           "player": {"state": state, "center": center}})
        output = {"schemaVersion": 1, "studySeed": original.SEED,
                  "sourceStudySeed": original.SEED, "runId": run,
                  "partition": "development", "frames": frames}
        path = self.labels / f"{run}-player-points.json"
        path.write_text(json.dumps(output))
        return path

    def test_unlabelledFrozenOriginals_ReturnNotReadyNeverFabricateHumanTruth(self):
        report = audit(self.sources, self.review)
        self.assertEqual("NOT_READY_NO_PRIVATE_HUMAN_LABELS", report["status"])
        self.assertEqual(54, report["unknownObservations"])
        self.assertEqual(0, report["reviewedObservations"])
        self.assertFalse(report["playerIdentityAccuracyEvaluated"])
        self.assertFalse(report["m237FullOriginalSequenceReady"])

    def test_partialOwnerExport_NeverTreatsMissingRunsAsAbsent(self):
        self.export("m238-a", visible=8, absent=3)
        report = audit(self.sources, self.review, self.labels)
        self.assertEqual("NOT_READY_PARTIAL_OWNER_EXPORTS", report["status"])
        self.assertEqual(11, report["reviewedObservations"])
        self.assertEqual(43, report["unknownObservations"])
        self.assertFalse(report["runs"]["m238-b"]["exportReceived"])

    def test_allOwnerExports_WithUnknown_RemainNotReady(self):
        for run in original.RUNS:
            self.export(run, visible=8, absent=3)
        report = audit(self.sources, self.review, self.labels)
        self.assertEqual("NOT_READY_UNKNOWN_OWNER_LABELS", report["status"])
        self.assertEqual(33, report["reviewedObservations"])
        self.assertEqual(21, report["unknownObservations"])

    def test_all54DefinitiveExport_AdmittedButCannotDeclareM237Ready(self):
        for run in original.RUNS:
            self.export(run, visible=12, absent=6)
        report = audit(self.sources, self.review, self.labels)
        self.assertEqual("PRIVATE_BLIND_DEVELOPMENT_REFERENCES_ADMITTED", report["status"])
        self.assertEqual(54, report["reviewedObservations"])
        self.assertFalse(report["m237FullOriginalSequenceReady"])
        self.assertFalse(report["liveAgentChanged"])
        self.assertNotIn("center", str(report))

    def test_swappedScreenshotSha_RejectsOwnerLabelLeakage(self):
        path = self.export("m238-c")
        d = json.loads(path.read_text())
        d["frames"][0]["screenshotSha256"] = "f" * 64
        path.write_text(json.dumps(d))
        with self.assertRaisesRegex(ValueError, "frozen original SHA"):
            audit(self.sources, self.review, self.labels)

    def test_outOfBoundsPointAndBooleanCoordinates_Reject(self):
        path = self.export("m238-b", visible=1)
        d = json.loads(path.read_text())
        for x in (390, True):
            d["frames"][0]["player"]["center"]["x"] = x
            path.write_text(json.dumps(d))
            with self.assertRaisesRegex(ValueError, "original 390x844"):
                audit(self.sources, self.review, self.labels)

    def test_absentWithPlayerCenter_RejectsInventedTruth(self):
        path = self.export("m238-c", absent=1)
        d = json.loads(path.read_text())
        d["frames"][0]["player"]["center"] = {"x": 80, "y": 120}
        path.write_text(json.dumps(d))
        with self.assertRaisesRegex(ValueError, "null center"):
            audit(self.sources, self.review, self.labels)

    def test_wrongRunAndStudySeed_Reject(self):
        path = self.export("m238-a")
        d = json.loads(path.read_text())
        d["sourceStudySeed"] = "wrong"
        path.write_text(json.dumps(d))
        with self.assertRaisesRegex(ValueError, "frozen source seed"):
            audit(self.sources, self.review, self.labels)

    def test_duplicateJSONKeysAndUnrecognizedExtraExport_Reject(self):
        path = self.export("m238-a")
        path.write_text(path.read_text().replace('"runId": "m238-a"',
                                                '"runId": "m238-a", "runId": "m238-a"'))
        with self.assertRaisesRegex(ValueError, "Ambiguous duplicate key"):
            audit(self.sources, self.review, self.labels)
        path.unlink()
        (self.labels / "owner-private-other.json").write_text("{}")
        with self.assertRaisesRegex(ValueError, "unrecognized"):
            audit(self.sources, self.review, self.labels)

    def test_identicalPngAcrossRuns_RejectsContradictoryDefinitiveOwnerLabels(self):
        frozen = json.loads((self.review / "study-manifest.json").read_text())
        from_a = frozen["runs"][0]["images"][0]
        from_b = frozen["runs"][1]["images"][0]
        a = self.sources / "m238-a" / from_a["file"]
        b = self.sources / "m238-b" / from_b["file"]
        b.write_bytes(a.read_bytes())
        shutil.rmtree(self.review)
        original.freeze(self.sources, self.review)
        self.export("m238-a", visible=1)
        other = self.export("m238-b", visible=1)
        good = audit(self.sources, self.review, self.labels)
        self.assertGreaterEqual(good["duplicateObservationsCompared"], 1)
        record = json.loads(other.read_text())
        record["frames"][0]["player"] = {"state": "absent", "center": None}
        other.write_text(json.dumps(record))
        with self.assertRaisesRegex(ValueError, "Conflicting definitive human truth"):
            audit(self.sources, self.review, self.labels)
        record["frames"][0]["player"] = {"state": "unknown", "center": None}
        other.write_text(json.dumps(record))
        unknown = audit(self.sources, self.review, self.labels)
        self.assertEqual("NOT_READY_PARTIAL_OWNER_EXPORTS", unknown["status"])

    def test_frozenOriginalEditedAfterReview_RejectsBeforeOpeningLabels(self):
        self.export("m238-a", visible=1)
        frame = next((self.review / "annoteren" / "m238-a").glob("frame-*.png"))
        frame.write_bytes(b"spoof")
        with self.assertRaises((OSError, ValueError)):
            audit(self.sources, self.review, self.labels)


if __name__ == "__main__":
    unittest.main()
