"""Synthetic-only M2.30 research; never include owner PNGs, coordinates or labels."""
import unittest
import hashlib
import numpy as np
from tools.m230 import identity_study as m


def fake_runs():
    by = {}
    for index, run in enumerate(m.RUNS):
        frames = []
        for step in range(18):
            rgb = np.full((844, 390, 3), (11+index*5+step)%255, dtype=np.uint8)
            rgb[800:812, 340:352, :] = ((index+step)*11)%255
            visible = step >= 4
            center = {'x': 0 if step%3 == 0 else (389 if step%3 == 1 else 195), 'y': 422} if visible else None
            if visible:
                x = center['x']
                y = center['y']
                rgb[y-7:y+8, max(0, x-7):min(390, x+8), :] = np.asarray([239, 190, 71], dtype=np.uint8)
            frames.append({'run': run, 'step': step, 'state': 'visible' if visible else 'absent',
                           'center': center, 'sha': hashlib.sha256(rgb.tobytes()).hexdigest(),
                           'rgb': rgb})
        by[run] = frames
    return by


class AppearanceStudyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.frames = fake_runs()

    def test_descriptor_exact_dimensions_finite_at_both_screen_edges(self):
        rgb = self.frames['dev-r'][8]['rgb']
        shape = m.descriptor(m.padded(rgb), 0, 0)
        border = m.descriptor(m.padded(rgb), 389, 843)
        self.assertEqual((288,), shape.shape)
        self.assertTrue(np.isfinite(shape).all() and np.isfinite(border).all())

    def test_no_pixel_coordinate_in_patch_features(self):
        rgb = np.zeros((844, 390, 3), dtype=np.uint8)
        rgb[392:440, 120:168, :] = [145, 12, 187]
        rgb[492:540, 220:268, :] = [145, 12, 187]
        pad = m.padded(rgb)
        np.testing.assert_allclose(m.descriptor(pad, 144, 416),
                                   m.descriptor(pad, 244, 516), atol=1e-6)

    def test_whole_run_and_exact_sha_exclusion_before_fit_and_cal(self):
        data = {k: list(v) for k, v in self.frames.items()}
        dup = dict(data['dev-r'][0])
        dup['run'] = 'dev-s'
        data['dev-s'][0] = dup
        test, fit, cal, _ = m.fold_partition(data, 'dev-r')
        forbidden = {f['sha'] for f in test}
        self.assertFalse(forbidden & {f['sha'] for f in fit+cal})
        self.assertEqual(set(f['state'] for f in cal), {'absent'})
        self.assertGreaterEqual(len(cal), 3)

    def test_no_repeated_true_absent_calibration_image(self):
        data = {k: list(v) for k, v in self.frames.items()}
        data['dev-s'][2] = dict(data['dev-s'][1], step=2)
        _, fit, cal, _ = m.fold_partition(data, 'dev-r')
        self.assertEqual(len({f['sha'] for f in cal}), len(cal))
        self.assertFalse(set(f['sha'] for f in fit) & set(f['sha'] for f in cal))

    def test_reject_conflicting_duplicate_original_sha(self):
        data = {k: list(v) for k, v in self.frames.items()}
        bad = dict(data['dev-r'][5], state='absent', center=None)
        data['dev-r'][6] = bad
        with self.assertRaisesRegex(ValueError, 'Contradictory identical'):
            m.fold_partition(data, 'dev-r')

    def test_no_unknown_imputed_as_human_absent(self):
        frames = list(self.frames['dev-r'])
        frames[1] = dict(frames[1], state='unknown', center=None)
        report = m.confusion(frames, [{'present': False, 'center': None} for _ in frames])
        self.assertEqual(3, report['absent'])
        self.assertEqual(14, report['visible'])

    def test_too_few_distinct_absent_calibration_images_is_not_ready(self):
        cal = [self.frames['dev-r'][0], self.frames['dev-r'][1]]
        with self.assertRaisesRegex(ValueError, 'NOT_READY'):
            m.fit_absent_threshold(cal, {}, grid=((195, 422),))

    def test_fit_is_numeric_and_reproducible_with_no_test_images(self):
        _, fit, cal, _ = m.fold_partition(self.frames, 'dev-r')
        a = m.fit_model(fit)
        b = m.fit_model(fit)
        np.testing.assert_allclose(a['weights'], b['weights'])
        self.assertTrue(np.isfinite(a['weights']).all())
        grid = ((0, 422), (195, 422), (389, 422))
        threshold = m.fit_absent_threshold(cal, a, grid=grid)
        self.assertTrue(np.isfinite(threshold))
        pred = m.decide(self.frames['dev-r'][11], a, threshold, grid=grid)
        self.assertIsInstance(pred['present'], bool)
        self.assertTrue(pred['center'] is None or pred['center'] in grid)

    def test_failed_localization_and_absent_false_presence_are_separate(self):
        truth = [self.frames['dev-r'][0], self.frames['dev-r'][7], self.frames['dev-r'][8]]
        predictions = [{'present': True, 'center': (195, 422)},
                       {'present': True, 'center': (195, 422)},
                       {'present': False, 'center': None}]
        metrics = m.confusion(truth, predictions)
        self.assertEqual((2, 1, 1, 1, 1),
                         (metrics['visible'], metrics['absent'], metrics['falsePresence'],
                          metrics['missedVisible'], metrics['presentButWrongLocation']))


if __name__ == '__main__':
    unittest.main()
