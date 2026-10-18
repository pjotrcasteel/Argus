"""No real sources, no private owner labels, no seal access."""
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from tools.m232 import capture_provenance as m
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_freeze_new_development import FreshDevelopmentFreezeTests


class ProvenanceTests(unittest.TestCase):
    def setUp(self):
        self.fixture = FreshDevelopmentFreezeTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.tearDown)
        self.root=self.fixture.inputs
        for run in m.RUNS:
            file=self.root/run/'run-manifest.json'
            obj=json.loads(file.read_text())
            obj['profileSha256']='a'*64
            obj['gameUrl']=m.URL
            file.write_text(json.dumps(obj))

    def test_three_distinct_complete_original_browser_traces(self):
        report=m.verify_all(self.root)
        self.assertEqual(3,len(report))
        self.assertEqual(3,len({x['traceSha256'] for x in report}))
        self.assertEqual({42}, {x['frames'] for x in report})

    def test_process_failure_cannot_be_promoted_to_success(self):
        path=self.root/'dev-v'/'run-manifest.json'
        raw=json.loads(path.read_text())
        raw['runnerExitCode']=1
        path.write_text(json.dumps(raw))
        with self.assertRaisesRegex(ValueError,'Invalid actual real-game'):
            m.verify_all(self.root)

    def test_source_preview_png_must_exist(self):
        (self.root/'dev-u'/'frame-0009.png').unlink()
        with self.assertRaisesRegex(ValueError,'Missing genuine'):
            m.verify_all(self.root)

    def test_evidence_gate_cannot_be_implied(self):
        path=self.root/'dev-w'/'run-manifest.json'
        raw=json.loads(path.read_text())
        raw['visualEvidenceGate']=None
        path.write_text(json.dumps(raw))
        with self.assertRaisesRegex(ValueError,'Invalid actual'):
            m.verify_all(self.root)

    def test_identical_trace_source_runs_refused(self):
        source=self.root/'dev-u'/'trace.jsonl'
        dst=self.root/'dev-w'/'trace.jsonl'
        dst.write_bytes(source.read_bytes())
        path=self.root/'dev-w'/'run-manifest.json'
        obj=json.loads(path.read_text())
        obj['traceSha256']=hashlib.sha256(dst.read_bytes()).hexdigest()
        path.write_text(json.dumps(obj))
        with self.assertRaisesRegex(ValueError,'Three independent'):
            m.verify_all(self.root)


if __name__=='__main__':
    unittest.main()
