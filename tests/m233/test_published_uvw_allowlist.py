"""M2.33 synthetic only: verify 54 public targets, withhold 54 authentic contexts."""
import hashlib
import sys
import tempfile
import unittest
from pathlib import Path
from zipfile import ZipFile

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
sys.path.insert(0, str(Path(__file__).resolve().parents[2]/"tests/m232"))
from test_freeze_new_development import FreshDevelopmentFreezeTests
from tools.m232.freeze_new_development import freeze
from tools.m233 import publish_approved_uvw as public


class PublicAllowlistTests(unittest.TestCase):
    def setUp(self):
        self.fake = FreshDevelopmentFreezeTests()
        self.fake.setUp()
        self.root = self.fake.root
        self.source = self.root/"frozen"
        freeze(self.fake.inputs, self.source)
        self.archive = self.root/"fictional.zip"
        self.write_zip()
        self.before = public.ARCHIVE_SHA256
        public.ARCHIVE_SHA256 = hashlib.sha256(self.archive.read_bytes()).hexdigest()

    def tearDown(self):
        public.ARCHIVE_SHA256 = self.before
        self.fake.tearDown()

    def write_zip(self, extra=None):
        with ZipFile(self.archive, "w") as z:
            for p in sorted(self.source.rglob("*")):
                if p.is_file():
                    z.write(p, p.relative_to(self.source).as_posix())
            if extra:
                z.writestr(*extra)

    def test_only_targets_and_blank_html_can_become_public(self):
        data, original_sha = public.read_original(self.archive)
        self.assertEqual(len(data), 57)
        self.assertEqual(len([p for p in data if p.endswith(".png")]), 54)
        self.assertEqual(len([p for p in data if p.endswith(".html")]), 3)
        self.assertEqual(set(original_sha), set(public.RUNS))
        self.assertTrue(all(len(x)==18 for x in original_sha.values()))
        self.assertFalse(any("/context/" in p or p.startswith("context/") for p in data))
        self.assertFalse(any("dev-o" in p or "run-01" in p for p in data))

    def test_extra_owner_labels_or_prospectives_are_rejected_even_when_rehashed(self):
        for path in ("private-owner-export.json", "annoteren/dev-o/frame-0001.png",
                     "context/dev-p/prev-for-0001.png", "trace.jsonl"):
            self.write_zip((path, b"synthetic forbidden private asset"))
            public.ARCHIVE_SHA256 = hashlib.sha256(self.archive.read_bytes()).hexdigest()
            with self.assertRaisesRegex(ValueError, "Unexpected private/held-out member"):
                public.read_original(self.archive)

    def test_malformed_private_source_context_fails_closed(self):
        with ZipFile(self.archive) as z:
            names=z.namelist()
        self.assertEqual(113,len(names))
        bad=self.source/"context"/"dev-u"
        name=next(bad.glob("*.png"))
        name.write_bytes(b"fake context")
        self.write_zip()
        public.ARCHIVE_SHA256 = hashlib.sha256(self.archive.read_bytes()).hexdigest()
        with self.assertRaisesRegex(ValueError, "private original temporal context"):
            public.read_original(self.archive)

    def test_archive_change_without_repin_is_rejected(self):
        public.ARCHIVE_SHA256 = "0"*64
        with self.assertRaisesRegex(ValueError, "checksum"):
            public.read_original(self.archive)


if __name__ == "__main__":
    unittest.main()
