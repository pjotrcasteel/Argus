"""Fully artificial candidate scores; never any genuine prospective or owner pixel."""
import hashlib
import unittest
import numpy as np
from tools.m232 import relative_presence as m


class RelativePresenceTests(unittest.TestCase):
    def setUp(self):
        self.centers=[(x*80,0) for x in range(5)]

    def test_dominant_object_but_low_background_contrast(self):
        x=m.image_evidence([1.,1.,1.,1.,1.01],self.centers)
        self.assertGreaterEqual(x['evidence'],0.)
        self.assertLess(x['evidence'],.05)

    def test_winner_against_background_and_independent_competitor(self):
        x=m.image_evidence([9.,1.,1.,1.,0.],self.centers)
        self.assertEqual((0,0),x['center'])
        self.assertGreater(x['evidence'],0.)
        self.assertEqual(0,x['winnerIndex'])

    def test_hud_and_player_equally_high_must_not_be_confident(self):
        x=m.image_evidence([8.,8.,0.,0.,0.],self.centers)
        self.assertEqual(0,x['evidence'])
        self.assertLessEqual(x['objectDominance'],0.)

    def test_absolute_logit_shift_changes_nothing(self):
        a=m.image_evidence([4.,3.,1.,0.,-3.],self.centers)
        b=m.image_evidence([104.,103.,101.,100.,97.],self.centers)
        self.assertAlmostEqual(a['evidence'],b['evidence'],places=12)

    def test_disallow_nonfinite_scan_or_invalid_shape(self):
        with self.assertRaisesRegex(ValueError,'Nonfinite'):
            m.image_evidence([1.,float('nan'),2.],[(0,0),(80,0),(160,0)])

    def test_calibration_requires_distinct_whole_absent_shas_and_runs(self):
        rows=[('dev-u', hashlib.sha256(f'u{i}'.encode()).hexdigest(),.4+i*.1) for i in range(4)]
        with self.assertRaisesRegex(ValueError,'NOT_READY'):
            m.calibrate_absent(rows)
        rows[3]=('dev-v', rows[3][1],rows[3][2])
        threshold=m.calibrate_absent(rows)
        self.assertAlmostEqual(.72,threshold)
        with self.assertRaisesRegex(ValueError,'distinct'):
            m.calibrate_absent(rows+rows[:1])

    def test_weak_evidence_and_equal_threshold_abstain(self):
        p={'evidence':.25,'center':(389,843)}
        self.assertFalse(m.decide(p,.25)['present'])
        self.assertIsNone(m.decide(p,.25)['center'])
        self.assertEqual((389,843),m.decide(p,.20)['center'])

    def test_run_gate_never_treats_unknown_as_absent(self):
        truth=[{'state':'visible','center':{'x':0,'y':400}} for _ in range(8)]
        truth += [{'state':'absent','center':None} for _ in range(3)]
        truth += [{'state':'unknown','center':None} for _ in range(7)]
        predictions=[{'present':True,'center':(0,400)} for _ in range(8)]
        predictions += [{'present':False,'center':None} for _ in range(10)]
        result=m.whole_run_gate(truth,predictions)
        self.assertEqual('INVESTIGATE_FURTHER',result['status'])
        self.assertEqual((8,3,7),tuple(result[k] for k in ('visible','absent','unknown')))

    def _fresh_synthetic_review_rows(self):
        return {run:[{'run':run,'step':i,
                      'sha':hashlib.sha256(f'{run}/{i}'.encode()).hexdigest(),
                      'state':'absent' if i<4 else 'visible',
                      'center':None if i<4 else {'x':389,'y':421}}
                     for i in range(18)] for run in ('dev-u','dev-v','dev-w')}

    def test_whole_run_out_split_and_calibration_sha_are_strictly_disjoint(self):
        data=self._fresh_synthetic_review_rows()
        fold=m.split_human_development_runs(data,'dev-u')
        self.assertEqual('dev-u',fold['heldOutRun'])
        self.assertEqual(18,len(fold['heldOut']))
        self.assertEqual(4,len(fold['calibration']))
        self.assertEqual({'dev-v','dev-w'},set(x['run'] for x in fold['calibration']))
        train={f['sha'] for f in fold['fit']}
        calibration={f['sha'] for f in fold['calibration']}
        held={f['sha'] for f in fold['heldOut']}
        self.assertFalse((train&calibration)|(held&(train|calibration)))

    def test_test_png_byte_duplicate_is_blocked_in_other_run_fit(self):
        data=self._fresh_synthetic_review_rows()
        data['dev-v'][6]=dict(data['dev-v'][6],sha=data['dev-u'][6]['sha'])
        fold=m.split_human_development_runs(data,'dev-u')
        self.assertEqual(1,fold['sharedExactSourceShaExcluded'])
        self.assertNotIn(data['dev-u'][6]['sha'],{f['sha'] for f in fold['fit']})

    def test_inadequate_absent_fit_or_calibration_fail_closed(self):
        data=self._fresh_synthetic_review_rows()
        for row in data['dev-v'][:4]:
            row['state']='unknown'
        with self.assertRaisesRegex(ValueError,'NOT_READY'):
            m.split_human_development_runs(data,'dev-u')

    def test_insufficient_genuine_absent_is_not_ready(self):
        truth=[{'state':'visible','center':{'x':195,'y':422}} for _ in range(16)]
        truth += [{'state':'absent','center':None} for _ in range(2)]
        preds=[{'present':False,'center':None} for _ in truth]
        self.assertEqual('NOT_READY',m.whole_run_gate(truth,preds)['status'])


if __name__=='__main__':
    unittest.main()
