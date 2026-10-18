"""Only synthetic PNGs and synthetic reviews. NEVER contain owner references."""
import hashlib
import io
import json
import tempfile
import unittest
import zipfile
from pathlib import Path

from PIL import Image
from tools.m229.evaluate_temporal_references import evaluate, FROZEN_STUDY_SEED, RUNS


def fake_png(seed):
    image = Image.new('RGB', (390, 844), (seed % 256, (seed // 256) % 256, 23))
    output = io.BytesIO()
    image.save(output, format='PNG')
    return output.getvalue()


def fake_analyzer(old, current, step, prior_tracks=()):
    points = ([{'x': 195, 'y': 422, 'novelty': .5}] if step % 2 == 0 else [])
    return ({'status':'SOURCE_ONLY_CANDIDATES_NOT_PLAYERS', 'step':step,
             'registration':{'reliable':True, 'dx':0, 'dy':0},
             'proposals':points, 'tracks':[]}, [])


class TemporalReferenceTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.original = self.root/'original.zip'
        self.reviews = self.root/'labels'
        self.reviews.mkdir()
        members = {}
        runs = []
        for group, run in enumerate(RUNS):
            frames = []
            images = []
            priors = []
            for step in range(1, 19):
                target = fake_png(group*1000+step)
                predecessor = fake_png(group*1000+step+100)
                name = f'frame-{step:04d}.png'
                prior_name = f'context/{run}/prev-for-{step:04d}.png'
                sha = hashlib.sha256(target).hexdigest()
                members[f'annoteren/{run}/{name}'] = target
                members[prior_name] = predecessor
                images.append({'step':step,'file':name,'sha256':sha})
                priors.append({'targetStep':step,'priorStep':step-1,'file':prior_name,
                               'sha256':hashlib.sha256(predecessor).hexdigest()})
                state = 'visible' if step<=10 else 'absent'
                frames.append({'step':step,'screenshotSha256':sha,'player':{'state':state,
                               'center':{'x':195,'y':422} if state=='visible' else None}})
            runs.append({'runId':run,'images':images,'immediatePriorOriginalFrames':priors})
            members[f'annoteren/{run}/index.html'] = b'blank synthetic reviewer'
            self.write_labels(run, frames)
        members['index.html'] = b'<html>artificial fixture</html>'
        members['study-manifest.json'] = json.dumps({
            'schemaVersion':1,'seed':FROZEN_STUDY_SEED,'runIds':list(RUNS),
            'targetsPerRun':18,'originalTargetImages':54,'originalPrecedingContextImages':54,
            'prospectiveOriginalsAccessed':False,'runs':runs}).encode()
        self.members = members
        self.save_zip()

    def save_zip(self):
        with zipfile.ZipFile(self.original,'w',zipfile.ZIP_DEFLATED) as z:
            for name, body in self.members.items():
                z.writestr(name, body)
        self.sha = hashlib.sha256(self.original.read_bytes()).hexdigest()

    def write_labels(self, run, frames):
        (self.reviews/f'm227-{run}-player-points.json').write_text(json.dumps({
            'schemaVersion':1,'studySeed':FROZEN_STUDY_SEED,
            'sourceStudySeed':FROZEN_STUDY_SEED,'runId':run,
            'partition':'development','frames':frames}))

    def run_report(self):
        return evaluate(self.original, self.reviews, expected_archive_sha=self.sha,
                        analyzer=fake_analyzer)

    def test_no_go_proposal_not_identity_and_strict_denominators(self):
        report=self.run_report()
        self.assertEqual('NO_GO',report['overallGate'])
        self.assertEqual(54,report['uniqueSourceScreenshotDigests'])
        for item in report['runs']:
            self.assertEqual((10,8,5,4), (item['visibleReviewed'],item['absentReviewed'],
                                           item['top16PlayerCenterWithin35'],item['absentFramesWithCandidates']))
            self.assertEqual('NO_GO',item['gate'])
            self.assertNotIn('PRIVATE_PER_IMAGE_DETAIL_NEVER_COMMIT',item)

    def test_private_details_are_explicit_opt_in(self):
        report=evaluate(self.original,self.reviews,expected_archive_sha=self.sha,
                        analyzer=fake_analyzer,private_details=True)
        self.assertEqual(18,len(report['runs'][0]['PRIVATE_PER_IMAGE_DETAIL_NEVER_COMMIT']))

    def test_source_archive_pin_tamper_rejected(self):
        with self.assertRaisesRegex(ValueError,'archive SHA'):
            evaluate(self.original,self.reviews,analyzer=fake_analyzer)

    def test_changed_human_step_or_screenshot_sha_rejected(self):
        path=self.reviews/'m227-dev-s-player-points.json'
        d=json.loads(path.read_text())
        d['frames'][4]['screenshotSha256']='0'*64
        path.write_text(json.dumps(d))
        with self.assertRaisesRegex(ValueError,'chronology differs'):
            self.run_report()

    def test_bogus_absent_center_rejected(self):
        path=self.reviews/'m227-dev-r-player-points.json'
        d=json.loads(path.read_text())
        d['frames'][12]['player']['center']={'x':195,'y':422}
        path.write_text(json.dumps(d))
        with self.assertRaisesRegex(ValueError,'No invented coordinate'):
            self.run_report()

    def test_out_of_bounds_review_rejected(self):
        path=self.reviews/'m227-dev-t-player-points.json'
        d=json.loads(path.read_text())
        d['frames'][0]['player']['center']['x']=390
        path.write_text(json.dumps(d))
        with self.assertRaisesRegex(ValueError,'original-pixel'):
            self.run_report()

    def test_unknown_stays_not_evaluated(self):
        for run in RUNS:
            path=self.reviews/f'm227-{run}-player-points.json'
            d=json.loads(path.read_text())
            for frame in d['frames']:
                frame['player']={'state':'unknown','center':None}
            path.write_text(json.dumps(d))
        self.assertEqual('NOT_EVALUATED',self.run_report()['overallGate'])

    def test_extra_private_member_rejected_even_when_archive_hash_authorized(self):
        self.members['owner-labels.json'] = b'{}'
        self.save_zip()
        with self.assertRaisesRegex(ValueError,'archive membership'):
            self.run_report()


if __name__=='__main__':
    unittest.main()
