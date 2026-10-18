"""100% synthetic original-sized RGB screens: no owner assets, sources or labels."""
import hashlib
import unittest

import numpy as np

from tools.m230 import identity_study as app
from tools.m235.causal_identity import Candidate
from tools.m236 import contrastive_identity as m


def invented(run, step, state="visible"):
    rgb=np.full((844,390,3),24,dtype=np.uint8)
    # Static screen HUD lookalike (the learned player cannot simply choose
    # the first bright pixel/shape without genuine training references).
    rgb[60:84,10:34,:]=[20,180,220]
    center={"x":80,"y":420} if state=="visible" else None
    if center is not None:
        rgb[414:426,74:86,:]=[210,55,45]
        rgb[412:428,78:82,:]=[240,160,65]
    sig=hashlib.sha256(rgb.tobytes()+f"{run}/{step}".encode()).hexdigest()
    return {"run":run,"step":step,"sha":sig,"state":state,
            "center":center,"rgb":rgb}


def fit_rows():
    return [invented(run,i,"absent" if i<4 else "visible")
            for run in ("dev-art-a","dev-art-b") for i in range(18)]


class ContrastiveIdentityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.training=fit_rows()
        cls.model=m.fit_metric(cls.training)

    def test_metric_learns_bounded_diagonal_weights_and_original_positive_anchors(self):
        learner=self.model
        self.assertEqual(m.MODEL_ID,learner["modelId"])
        self.assertEqual(28,learner["fitPositiveScreenshots"])
        self.assertEqual(2,len(learner["fitRuns"]))
        self.assertEqual((288,),learner["weights"].shape)
        self.assertTrue((learner["weights"]>0).all())
        self.assertAlmostEqual(1.,float(learner["weights"].mean()),places=10)

    def test_true_training_player_has_higher_shape_similarity_than_plain_background(self):
        screen=invented("independent-example",123)["rgb"]
        player=app.descriptor(app.padded(screen),80,420)
        blank=app.descriptor(app.padded(screen),300,600)
        result=m.score_features(np.stack([player,blank]),self.model)
        self.assertGreater(result[0],result[1])

    def test_weights_do_not_depend_on_training_input_order(self):
        learned=m.fit_metric(list(reversed(self.training)))
        np.testing.assert_allclose(learned["weights"],self.model["weights"])
        np.testing.assert_allclose(learned["centroid"],self.model["centroid"])
        self.assertEqual(m.model_digest(learned, -.2),
                         m.model_digest(self.model, -.2))

    def test_real_original_sized_scan_includes_screen_edges_and_local_pixel_change(self):
        source=invented("arbitrary",42)["rgb"]
        old=source.copy()
        old[414:428,74:86,:]=[24,24,24]
        grid=((0,420),(80,420),(195,420),(389,420))
        found=m.source_candidates(old,source,self.model,grid=grid,limit=4)
        self.assertEqual(4,len(found))
        hero=next(x for x in found if x.x==80)
        self.assertGreater(hero.raw_change,0.)
        self.assertLessEqual(max(x.raw_change for x in found),1.)
        self.assertEqual(0.,next(x for x in found if x.x==389).raw_change)

    def test_calibration_must_have_four_distinct_real_absences_from_two_runs(self):
        controls=[invented(f"control-{i%2}",i,"absent") for i in range(4)]
        threshold=m.calibrate_absent(controls,self.model,grid=((0,420),(80,420),(389,420)))
        self.assertTrue(np.isfinite(threshold))
        with self.assertRaisesRegex(ValueError,"NOT_READY"):
            m.calibrate_absent(controls[:3],self.model,grid=((0,420),(80,420),(389,420)))
        with self.assertRaisesRegex(ValueError,"NOT_READY"):
            m.calibrate_absent([dict(x,run="one") for x in controls],self.model,
                               grid=((0,420),(80,420),(389,420)))

    def test_original_sha_cannot_overlap_training_and_absent_calibration(self):
        frames=[invented(f"control-{i%2}",i,"absent") for i in range(4)]
        frames[0]["sha"]=self.training[0]["sha"]
        with self.assertRaisesRegex(ValueError,"NOT_READY"):
            m.calibrate_absent(frames,self.model,grid=((0,420),(80,420),(389,420)))

    def test_repeat_training_original_sha_refused(self):
        samples=fit_rows()
        samples[1]["sha"]=samples[0]["sha"]
        with self.assertRaisesRegex(ValueError,"unique"):
            m.fit_metric(samples)

    def test_training_must_include_real_absent_and_two_runs(self):
        all_visible=[f for f in fit_rows() if f["state"]=="visible"]
        with self.assertRaisesRegex(ValueError,"NOT_READY"):
            m.fit_metric(all_visible)
        one_run=[f for f in fit_rows() if f["run"]=="dev-art-a"]
        with self.assertRaisesRegex(ValueError,"two development|>=2"):
            m.fit_metric(one_run)

    def test_unknown_cannot_be_used_as_absent_training_evidence(self):
        sources=fit_rows()
        sources[0]["state"]="unknown"
        with self.assertRaisesRegex(ValueError,"verified"):
            m.fit_metric(sources)

    def test_zero_proposals_must_abstain_without_fabricated_player_center(self):
        answer=m.classify_shadow([],threshold=-.1)
        self.assertEqual("SOURCE_ONLY_ABSTAIN",answer["status"])
        self.assertIsNone(answer["verifiedPlayerCenter"])
        self.assertIsNone(answer["playerPresent"])

    def test_two_similar_player_looking_objects_force_ambiguity(self):
        proposals=[Candidate(0,420,(1.,),.9,.2),
                   Candidate(389,420,(0.,1.),.88,.3)]
        answer=m.classify_shadow(proposals,threshold=-1.)
        self.assertEqual("LOOKALIKE_AMBIGUITY_ABSTAIN",answer["status"])
        self.assertIsNone(answer["candidate"])

    def test_unique_candidate_is_explicitly_unverified(self):
        proposals=[Candidate(80,420,(1.,),.9,.2),
                   Candidate(320,420,(0.,),.5,.2)]
        answer=m.classify_shadow(proposals,threshold=.1)
        self.assertEqual("UNVERIFIED_APPEARANCE_CANDIDATE",answer["status"])
        self.assertEqual({"x":80,"y":420},answer["candidate"])
        self.assertIsNone(answer["playerPresent"])
        self.assertIsNone(answer["verifiedPlayerCenter"])

    def test_threshold_ties_reject_without_creating_player_presence(self):
        proposals=[Candidate(80,420,(1.,),.5,.2)]
        answer=m.classify_shadow(proposals,threshold=.5)
        self.assertEqual("SOURCE_ONLY_ABSTAIN",answer["status"])

    def test_score_features_invalid_dimensions_rejected(self):
        with self.assertRaisesRegex(ValueError,"dimensions"):
            m.score_features(np.ones((2,37)),self.model)


if __name__=="__main__":
    unittest.main()
