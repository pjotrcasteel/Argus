"""No real owner samples. Category correctness only on artificial summaries."""
import unittest
from tools.m231.private_identity_failures import categorize


class FailureCategoryTests(unittest.TestCase):
    def test_missing_on_true_visible_near_top_is_abstention(self):
        x = categorize('visible', {'x': 389, 'y': 421}, (389, 422), .1, .2, -.1)
        self.assertEqual('abstained_visible', x['kind'])
        self.assertTrue(x['rawTopNearRealPlayer'])
        self.assertFalse(x['accepted'])

    def test_visible_far_accepted_is_wrong_location(self):
        x = categorize('visible', {'x': 0, 'y': 421}, (389, 421), .3, .2, -.2)
        self.assertEqual('wrong_location', x['kind'])
        self.assertFalse(x['rawTopNearRealPlayer'])

    def test_visible_right_is_correct_if_under_35px(self):
        x = categorize('visible', {'x': 389, 'y': 421}, (373, 429), .3, .2, -.1)
        self.assertEqual('located', x['kind'])
        self.assertTrue(x['rawTopNearRealPlayer'])

    def test_real_absence_with_high_score_is_false_candidate(self):
        x = categorize('absent', None, (195, 422), .9, .2)
        self.assertEqual('absent_false_candidate', x['kind'])
        self.assertIsNone(x['topToRealPixels'])

    def test_real_absence_rejected_is_correct(self):
        self.assertEqual('correctly_absent',
                         categorize('absent', None, (0, 0), .1, .2)['kind'])

    def test_unknown_does_not_become_absent(self):
        self.assertIsNone(categorize('unknown', None, (0, 0), .9, .2))

    def test_tied_score_never_falsely_accepted(self):
        self.assertFalse(categorize('visible', {'x': 0, 'y': 0}, (0, 0), .2, .2)['accepted'])


if __name__ == '__main__':
    unittest.main()
