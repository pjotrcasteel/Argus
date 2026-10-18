"""No real screenshots or owner truths: test allowlist with entirely artificial PNGs."""
import hashlib
import sys
import tempfile
import unittest
from pathlib import Path
from zipfile import ZipFile

sys.path.insert(0,str(Path(__file__).resolve().parents[2]))
sys.path.insert(0,str(Path(__file__).resolve().parents[2]/"tests/m223"))
from tools.m224 import publish_development_only as pub
from tools.m223.freeze_new_cohorts import original_frames
from test_new_cohorts import CohortTests


class PublicationTests(unittest.TestCase):
    def setUp(self):
        self.fixture=CohortTests()
        self.fixture.setUp()
        self.root=self.fixture.root
        self.input=self.root/"original-development"
        original_frames(self.fixture.inputs,"development",self.input)
        self.zip=self.root/"original-review.zip"
        self.write_zip()
        self.before=pub.SOURCE_ARCHIVE_SHA256
        pub.SOURCE_ARCHIVE_SHA256=hashlib.sha256(self.zip.read_bytes()).hexdigest()

    def tearDown(self):
        pub.SOURCE_ARCHIVE_SHA256=self.before
        self.fixture.tearDown()

    def write_zip(self,extra=None):
        with ZipFile(self.zip,"w") as z:
            for p in sorted(self.input.rglob("*")):
                if p.is_file():
                    z.write(p,p.relative_to(self.input).as_posix())
            if extra:
                z.writestr(*extra)

    def test_admit_only_54_unchanged_original_pngs_and_three_unknown_reviewers(self):
        images,sha=pub.admitted_originals(self.zip)
        self.assertEqual(57,len(images))
        self.assertEqual(set(sha),set(pub.RUNS))
        self.assertEqual(54,len([n for n in images if n.endswith(".png")]))
        self.assertEqual(3,len([n for n in images if n.endswith(".html")]))
        self.assertFalse(any(n.startswith("annoteren/dev-o") for n in images))
        self.assertFalse(any(n.endswith(".json") for n in images))
        self.assertTrue(all(len(x)==18 for x in sha.values()))

    def test_all_private_or_prospective_extra_files_fail_closed(self):
        for name in ("private-owner-labels.json",
                     "annoteren/dev-o/frame-0001.png",
                     "trace.jsonl"):
            self.write_zip((name,b"artificial private content"))
            pub.SOURCE_ARCHIVE_SHA256=hashlib.sha256(self.zip.read_bytes()).hexdigest()
            with self.assertRaisesRegex(ValueError,"Only 54 original frames"):
                pub.admitted_originals(self.zip)
        # Bad original source digest is separately rejected.
        pub.SOURCE_ARCHIVE_SHA256="0"*64
        with self.assertRaisesRegex(ValueError,"original M2.23"):
            pub.admitted_originals(self.zip)


if __name__=="__main__":
    unittest.main()
