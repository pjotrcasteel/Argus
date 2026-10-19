"""Synthetic tests for offline restoration of already captured M2.38 evidence.

No WyrmForge screenshot, owner label, prospective source or weights is stored.
"""
import json
import unittest
from pathlib import Path

from test_fresh_capture import FreshCaptureSyntheticTests
from tools.m238 import fresh_capture as m
from tools.m238.validate_archive import validate


class ArchivedReviewRevalidationTests(unittest.TestCase):
    def setUp(self):
        self.fixture = FreshCaptureSyntheticTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.tearDown)
        self.captures = self.fixture.captures
        self.review = self.fixture.root / "frozen"
        m.freeze(self.captures, self.review)

    def test_archived_independent_pngs_AndSourceHashes_ValidateWithoutRecapture(self):
        result = validate(self.captures, self.review)
        self.assertEqual("SOURCE_VERIFIED_REVIEW_NOT_EVALUATED", result["status"])
        self.assertEqual(54, result["originalTargets"])
        self.assertEqual(54, result["authenticPredecessors"])
        self.assertEqual(3, len(result["sourceRuns"]))
        self.assertEqual("NONE", result["humanLabels"])

    def test_corruptedReviewFrame_RejectsChangedImage(self):
        original = next((self.review / "annoteren" / "m238-a").glob("frame-*.png"))
        original.write_bytes(b"not an original screenshot")
        with self.assertRaises((ValueError, OSError)):
            validate(self.captures, self.review)

    def test_SourceScreenshotChangedAfterFreeze_RejectsStaleReview(self):
        image = next((self.captures / "m238-b").glob("frame-*.png"))
        image.write_bytes((self.captures / "m238-a" / image.name).read_bytes())
        with self.assertRaisesRegex(ValueError, "byte SHA|mismatch"):
            validate(self.captures, self.review)

    def test_PreviousFrameSwapped_RejectsFalseTemporalContext(self):
        original = next((self.review / "context" / "m238-c").glob("prev-for-*.png"))
        original.write_bytes((self.captures / "m238-a" / "frame-0000.png").read_bytes())
        with self.assertRaisesRegex(ValueError, "predecessor"):
            validate(self.captures, self.review)

    def test_FrozenSamplingStepEdited_RejectsSelectionLeakage(self):
        manifest = self.review / "study-manifest.json"
        data = json.loads(manifest.read_text())
        data["runs"][0]["images"][0]["step"] += 1
        manifest.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, "deterministic"):
            validate(self.captures, self.review)

    def test_AddedOwnerLabelsInArchive_RejectsUnexpectedFile(self):
        (self.review / "annoteren" / "m238-a" / "labels.json").write_text("{}")
        with self.assertRaisesRegex(ValueError, "unexpected files"):
            validate(self.captures, self.review)

    def test_PublishedOrAssumedVersion_RejectsUnverifiedScientificClaims(self):
        manifest = self.review / "study-manifest.json"
        data = json.loads(manifest.read_text())
        data["publicPublicationAuthorized"] = True
        data["gameVersion"] = "0.0.125"
        manifest.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, "protocol changed"):
            validate(self.captures, self.review)


if __name__ == "__main__":
    unittest.main()
