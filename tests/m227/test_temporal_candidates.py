"""Artificial scenes only: a drawn avatar-shaped blob and HUD are NOT real player evidence."""
import sys
import unittest
from pathlib import Path
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from tools.m227 import temporal_candidates as temporal


def artificial_pair(dx=16, dy=-8):
    rng = np.random.default_rng(2701)
    field = rng.integers(10, 210, size=(212, 98, 3), dtype=np.uint8)
    previous = np.repeat(np.repeat(field, 4, axis=0), 4, axis=1)[:844, :390].copy()
    current, _ = temporal.shift_with_valid(previous, dx, dy)
    # Fictitious stationary visual object, foreground against moving scene.
    previous[404:438, 177:207] = [249, 40, 22]
    current[404:438, 177:207] = [249, 40, 22]
    # Fictitious fixed-position UI can generate the SAME temporal hypothesis!
    previous[34:55, 22:84] = [15, 230, 20]
    current[34:55, 22:84] = [15, 230, 20]
    return previous, current


class TemporalTests(unittest.TestCase):
    def test_reject_nonoriginal_rgb_shape(self):
        with self.assertRaisesRegex(ValueError, "390x844"):
            temporal.registration(np.zeros((24, 20, 3), np.uint8),
                                  np.zeros((24, 20, 3), np.uint8))

    def test_estimate_original_scene_translation_without_oracle(self):
        old, new = artificial_pair()
        shift = temporal.registration(old, new)
        self.assertTrue(shift.reliable, str(shift))
        self.assertLessEqual(abs(shift.dx - 16), 4)
        self.assertLessEqual(abs(shift.dy + 8), 4)
        self.assertGreater(shift.peak_ratio, 1.08)

    def test_still_and_unreliable_frame_abstains_not_invents_a_player(self):
        blank = np.full((844, 390, 3), 30, dtype=np.uint8)
        shift = temporal.registration(blank, blank)
        self.assertFalse(shift.reliable)
        saliency = temporal.temporal_novelty(blank, blank, shift)
        self.assertEqual(float(saliency.max()), 0.)
        self.assertEqual(temporal.proposals(blank, saliency), [])
        old, new = artificial_pair()
        unreliable = temporal.Registration(16, -8, 1., False)
        self.assertEqual(float(temporal.temporal_novelty(old, new, unreliable).max()), 0.)

    def test_scene_change_and_hud_are_both_candidates_not_player_truth(self):
        old, new = artificial_pair()
        shift = temporal.registration(old, new)
        novelty = temporal.temporal_novelty(old, new, shift)
        found = temporal.proposals(new, novelty)
        self.assertGreater(float(novelty.max()), .10)
        self.assertGreater(len(found), 1)
        self.assertLessEqual(len(found), temporal.MAX_PROPOSALS)
        self.assertTrue(any(abs(p.x-190) <= 40 and abs(p.y-420) <= 40 for p in found))
        # The synthetic HUD wins this test. Motion is NOT player classification.
        self.assertLess(found[0].y, 100)
        report, tracks = temporal.analyze_pair(old, new, 1)
        self.assertEqual(report["status"], "SOURCE_ONLY_CANDIDATES_NOT_PLAYERS")
        self.assertNotIn("playerCenter", str(report))
        self.assertNotIn("playerState", str(report))
        self.assertEqual(len(tracks), len(report["proposals"]))

    def test_track_uses_appearance_and_time_not_coordinates_as_truth(self):
        same = tuple([.2] * 67)
        contrary = tuple([-.2] * 67)
        past = [temporal.Track(7, 100, 200, 2, 6, same, .4)]
        current = [temporal.Proposal(115, 207, .4, same)]
        matched = temporal.update_tracks(past, current, 7)
        self.assertEqual((matched[0].track_id, matched[0].age), (7, 3))
        fresh = temporal.update_tracks(
            past, [temporal.Proposal(115, 207, .3, contrary)], 7)
        self.assertNotEqual(fresh[0].track_id, 7)
        with self.assertRaisesRegex(ValueError, "Out-of-order"):
            temporal.update_tracks(past, current, 6)

    def test_registration_does_not_wrap_outside_source_image(self):
        canvas = np.ones((12, 15), dtype=np.float32)
        aligned, valid = temporal.shift_with_valid(canvas, 6, -4)
        self.assertEqual(aligned[0, 0], 0.)
        self.assertEqual(aligned[0, 6], 1.)
        self.assertFalse(valid[0, 0])
        self.assertTrue(valid[0, 6])


if __name__ == "__main__":
    unittest.main()
