"""M2.35 research tests are 100% invented visual hypotheses and RGB arrays."""
import hashlib
import unittest

import numpy as np

from tools.m235.causal_identity import (
    Candidate, CausalIdentityResearch, MODEL_ID, MAX_CANDIDATES,
    cosine, frame_hash, original_image_candidates,
)


def sha(number):
    return hashlib.sha256(str(number).encode()).hexdigest()


def candidate(x, y=421, *, vector=(1., .0, .1), score=4., change=.25):
    return Candidate(x, y, tuple(vector), score, change)


class CausalIdentitySyntheticTests(unittest.TestCase):

    def test_3_original_frames_can_form_unverified_hypothesis_never_player_identity(self):
        tracker = CausalIdentityResearch()
        results = [tracker.update("artificial-r1", i, sha(i), [candidate(10+4*i)])
                   for i in range(3)]
        self.assertEqual("INSUFFICIENT_CAUSAL_IDENTITY_EVIDENCE", results[1]["status"])
        self.assertEqual("UNVERIFIED_SOURCE_ONLY_IDENTITY_HYPOTHESIS", results[2]["status"])
        self.assertEqual(3, results[2]["candidate"]["age"])
        self.assertEqual(2, results[2]["candidate"]["changedTransitions"])
        self.assertEqual({"x":18,"y":421},
                         {k:results[2]["candidate"][k] for k in ("x","y")})
        self.assertIsNone(results[2]["verifiedPlayerCenter"])
        self.assertIsNone(results[2]["playerPresent"])
        self.assertFalse(results[2]["liveAgentChanged"])

    def test_static_high_scoring_hud_never_becomes_player(self):
        tracker=CausalIdentityResearch()
        for i in range(8):
            result=tracker.update("fake-hud",i,sha(i),[candidate(195,300,score=100.,change=0.)])
        self.assertEqual("INSUFFICIENT_CAUSAL_IDENTITY_EVIDENCE",result["status"])
        self.assertIsNone(result["candidate"])

    def test_stationary_player_without_visual_change_is_rightly_unresolved(self):
        tracker=CausalIdentityResearch()
        for i in range(4):
            result=tracker.update("fake-still-avatar",i,sha(i),
                                  [candidate(0,420,score=8.,change=0.)])
        self.assertEqual("INSUFFICIENT_CAUSAL_IDENTITY_EVIDENCE",result["status"])

    def test_competing_identical_monsters_must_abstain(self):
        tracker=CausalIdentityResearch()
        for i in range(3):
            result=tracker.update("lookalikes",i,sha(i),[
                candidate(45+i*2,score=6.,vector=(1.,0.,0.)),
                candidate(280+i*2,score=6.,vector=(0.,1.,0.))])
        self.assertEqual("MULTIPLE_LOOKALIKE_HYPOTHESES_ABSTAIN",result["status"])
        self.assertIsNone(result["candidate"])
        self.assertEqual(2,result["eligibleIdentityTracks"])

    def test_higher_rank_one_of_two_tracks_has_unverified_proposal(self):
        tracker=CausalIdentityResearch()
        for i in range(3):
            result=tracker.update("two-moving-objects",i,sha(i),[
                candidate(20+i*2,score=7.,vector=(1.,0.,0.)),
                candidate(260+i*2,score=1.,vector=(0.,1.,0.))])
        self.assertEqual("UNVERIFIED_SOURCE_ONLY_IDENTITY_HYPOTHESIS",result["status"])
        self.assertEqual(24,result["candidate"]["x"])
        self.assertIsNone(result["playerPresent"])

    def test_large_jump_and_feature_mismatch_break_continuity(self):
        for changing in ("far","appearance"):
            tracker=CausalIdentityResearch()
            tracker.update(changing,0,sha(1),[candidate(0,vector=(1.,0.))])
            tracker.update(changing,1,sha(2),
                           [candidate(300,vector=(1.,0.)) if changing=="far"
                            else candidate(1,vector=(0.,1.))])
            last=tracker.update(changing,2,sha(3),
                                [candidate(299,vector=(1.,0.)) if changing=="far"
                                 else candidate(2,vector=(0.,1.))])
            self.assertEqual("INSUFFICIENT_CAUSAL_IDENTITY_EVIDENCE",last["status"])

    def test_duplicate_original_pixel_sha_is_not_temporal_independent_evidence(self):
        tracker=CausalIdentityResearch()
        tracker.update("duplicate",0,sha(1),[candidate(10)])
        tracker.update("duplicate",1,sha(1),[candidate(12)])
        last=tracker.update("duplicate",2,sha(2),[candidate(14)])
        self.assertIsNone(last["candidate"])
        self.assertEqual("INSUFFICIENT_CAUSAL_IDENTITY_EVIDENCE",last["status"])

    def test_missing_original_step_resets_multi_frame_evidence(self):
        tracker=CausalIdentityResearch()
        tracker.update("missing",0,sha(1),[candidate(10)])
        tracker.update("missing",1,sha(2),[candidate(12)])
        last=tracker.update("missing",7,sha(3),[candidate(14)])
        self.assertEqual("INSUFFICIENT_CAUSAL_IDENTITY_EVIDENCE",last["status"])
        self.assertFalse(last["consecutiveOriginalFrames"])

    def test_whole_game_runs_must_never_share_track_ids_or_history(self):
        tracker=CausalIdentityResearch()
        for i in range(3):
            tracker.update("rA",i,sha(i),[candidate(10+i)])
        first=tracker.update("rB",0,sha(100),[candidate(12)])
        self.assertIsNone(first["candidate"])
        self.assertEqual(1,tracker.tracks[0].age)
        self.assertEqual(0,tracker.tracks[0].track_id)

    def test_old_screenshot_sha_does_not_match_claimed_predecessor(self):
        tracker=CausalIdentityResearch()
        tracker.update("sha",0,sha(1),[candidate(11)])
        tracker.update("sha",1,sha(2),[candidate(13)],previous_sha=sha(999))
        last=tracker.update("sha",2,sha(3),[candidate(15)],previous_sha=sha(2))
        self.assertIsNone(last["candidate"])
        self.assertEqual(2,tracker.tracks[0].age)

    def test_out_of_order_or_duplicate_step_fails_closed(self):
        tracker=CausalIdentityResearch()
        tracker.update("seq",2,sha(1),[candidate(1)])
        with self.assertRaisesRegex(ValueError,"out-of-order"):
            tracker.update("seq",2,sha(2),[candidate(2)])

    def test_one_old_identity_cannot_become_two_competitors(self):
        tracker=CausalIdentityResearch()
        tracker.update("one-to-many",0,sha(0),[candidate(50,vector=(1.,0.))])
        tracker.update("one-to-many",1,sha(1),[
            candidate(48,vector=(1.,0.)),candidate(60,vector=(1.,0.))])
        self.assertEqual(2,len(tracker.tracks))
        self.assertEqual([1,2],sorted(t.age for t in tracker.tracks))

    def test_at_edges_candidate_positions_are_valid(self):
        tracker=CausalIdentityResearch()
        for i in range(3):
            last=tracker.update("edge",i,sha(i),[
                candidate(389,843,score=5.,change=.9)])
        self.assertEqual((389,843),(last["candidate"]["x"],last["candidate"]["y"]))

    def test_numpy_original_pixels_sha_and_static_source_candidate_difference(self):
        previous=np.zeros((844,390,3),np.uint8)
        current=previous.copy()
        current[400:440,0:20,:]=[200,90,60]
        points=((0,421),(195,421),(389,421))
        shape=288
        fit={"mean":np.zeros(shape),"std":np.ones(shape),
             "weights":np.zeros(shape),"bias":1.}
        found=original_image_candidates(previous,current,fit,grid=points,limit=3)
        self.assertEqual(3,len(found))
        edge=next(c for c in found if c.x==0)
        center=next(c for c in found if c.x==195)
        self.assertGreater(edge.raw_change,.01)
        self.assertEqual(0.,center.raw_change)
        self.assertEqual(64,len(frame_hash(current)))

    def test_original_input_without_real_image_fails_closed(self):
        bad=np.zeros((843,390,3),np.uint8)
        with self.assertRaisesRegex(ValueError,"390x844"):
            frame_hash(bad)

    def test_no_nonfinite_or_empty_appearance_accepted(self):
        with self.assertRaises((ValueError, TypeError)):
            Candidate(15,420,(),2.,.3)
        with self.assertRaises((ValueError, TypeError)):
            Candidate(15,420,(1.,float("nan")),2.,.3)
        with self.assertRaisesRegex(ValueError,"finite"):
            cosine((1.,float("nan")),(1.,2.))

    def test_more_than_twelfth_candidate_cannot_be_injected(self):
        tracker=CausalIdentityResearch()
        with self.assertRaisesRegex(ValueError,"Bounded"):
            tracker.update("overflow",0,sha(0),[
                candidate(i*10) for i in range(MAX_CANDIDATES+1)])

    def test_never_claims_player_when_two_moving_hypotheses_are_confused(self):
        tracker=CausalIdentityResearch()
        for i in range(5):
            current=tracker.update("artificial",i,sha(i),[
                candidate(20+i,vector=(1.,0.),change=.8,score=2.),
                candidate(280+i,vector=(0.,1.),change=.8,score=2.2)])
        self.assertTrue(current["noPrivilegedTruthAccess"])
        self.assertFalse(current["liveAgentChanged"])
        self.assertIsNone(current["verifiedPlayerCenter"])
        self.assertIsNone(current["playerPresent"])


if __name__=="__main__":
    unittest.main()
