"""M2.43 synthetic-only append/preserve tests. Never bundle live screenshots in Git."""
import copy
import json
import tempfile
import unittest
from pathlib import Path

from tests.m238.test_fresh_capture import FreshCaptureSyntheticTests
from tools.m238.fresh_capture import freeze
from tools.m243.publish_approved_m238 import publish, verify, approved_base


class M243ApprovedDevelopmentPublicationTests(unittest.TestCase):
    def setUp(self):
        self.art = FreshCaptureSyntheticTests()
        self.art.setUp()
        self.addCleanup(self.art.tearDown)
        self.base = self.art.root / "public-old"
        self.base.mkdir()
        (self.base / ".nojekyll").write_bytes(b"")
        self.approved_ids = [f"dev-{s}" for s in "abcdefghijklmn" + "rstuvw"] + [
            "run-02", "dev-f-points", "dev-g-points", "dev-h-points"]
        # 24 prior reviewers, 324 images; no source/private originals.
        self.approved_ids = [
            "run-02", *[f"dev-{c}" for c in "abcdefghijklmn" + "rstuvw"],
            "dev-f-points", "dev-g-points", "dev-h-points"]
        self.assertEqual(24, len(self.approved_ids))
        for index, name in enumerate(self.approved_ids):
            directory = self.base / "annoteren" / name
            directory.mkdir(parents=True)
            (directory / "index.html").write_text(f"<html><body>{name}</body></html>")
            amount = 13 if index < 12 else 14
            for j in range(amount):
                (directory / f"frame-{j:04}.png").write_bytes(
                    f"synthetic-only-original-{index}-{j}".encode())
        self.assertEqual(324, len(list(self.base.glob("annoteren/*/frame-*.png"))))
        (self.base / "index.html").write_text(
            '<!doctype html><html><body><h1>previous reviews</h1><ul>'
            '<li><a href="annoteren/run-02/">old</a></li></ul></body></html>')
        (self.base / "published-development-manifest.json").write_text(
            json.dumps({"m232PointRoutes": [f"annoteren/dev-{s}/" for s in "uvw"],
                        "m232HumanTruth": "ALL_UNKNOWN",
                        "m232SourceStudySeed":
                            "argus-m232-independent-uvw-development-20261018-v1",
                        "m215ReviewConsent": "Owner granted standing consent for development frames"}))
        self.review = self.art.root / "private-review"
        freeze(self.art.captures, self.review)

    def test_publicationIsOnly54NewOriginalTargetsAndPreservesOldApproval(self):
        preserved = {p.relative_to(self.base).as_posix():p.read_bytes()
                     for p in self.base.rglob("*") if p.is_file() and
                     p.name not in ("index.html", "published-development-manifest.json")}
        state = publish(self.base, self.art.captures, self.review)
        self.assertEqual(3, len(state["routes"]))
        meta = verify(self.base)
        self.assertEqual(378, len(list(self.base.glob("annoteren/*/frame-*.png"))))
        self.assertEqual(27, len(list(self.base.glob("annoteren/*/index.html"))))
        self.assertEqual(54, meta["sourceFrames"])
        self.assertEqual(324, meta["oldOriginalFramesPreserved"])
        self.assertEqual(0, meta["originalPreviousScreenshotsPublished"])
        self.assertFalse(meta["ownerTruthPublished"])
        for path, raw in preserved.items():
            self.assertEqual(raw, (self.base / path).read_bytes(), path)
        with self.assertRaisesRegex(ValueError, "baseline"):
            publish(self.base, self.art.captures, self.review)

    def test_originalScreenshotChange_RefusesPublicationBeforeAnyPublicMutation(self):
        frame = next((self.review / "annoteren" / "m238-a").glob("*.png"))
        frame.write_bytes(b"forgery")
        with self.assertRaises((OSError, ValueError)):
            publish(self.base, self.art.captures, self.review)
        self.assertFalse((self.base / "annoteren" / "m238-a").exists())

    def test_unauthorizedExtraPreviousContext_DisallowsPublicSite(self):
        publish(self.base, self.art.captures, self.review)
        prior = self.base / "annoteren" / "m238-a" / "prev-for-0001.png"
        prior.write_bytes(b"forbidden")
        with self.assertRaisesRegex(ValueError, "Unexpected files"):
            verify(self.base)

    def test_sealedOrAddedOwnerLabels_DisallowsPublicSite(self):
        publish(self.base, self.art.captures, self.review)
        hidden = self.base / "annoteren" / "dev-o"
        hidden.mkdir()
        (hidden / "frame-0001.png").write_bytes(b"forbidden")
        with self.assertRaisesRegex(ValueError, "Unexpected original context"):
            verify(self.base)


if __name__ == "__main__":
    unittest.main()
