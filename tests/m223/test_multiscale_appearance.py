"""Only invented colored pixels and synthetic human references: no real screenshots/labels."""
import hashlib
import sys
import unittest
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from tools.m223 import multiscale_appearance as m223


def synthetic(run, step, state, x=180):
    image = np.zeros((844, 390, 3), dtype=np.uint8)
    image[:, :, :] = ((15+len(run)*7) % 100, (20+step*5) % 130, 32)
    c = None
    if state == "visible":
        c = {"x": x, "y": 422}
        image[416:428,max(0,x-9):min(390,x+9)] = [222,195,35]
        image[409:414,max(0,x-4):min(390,x+4)] = [245,228,45]
    return {"run": run, "step": step, "sha": hashlib.sha256(image.tobytes()).hexdigest(),
            "state": state, "center": c, "rgb": image}


class ModelTests(unittest.TestCase):
    def test_descriptor_246_finite_and_does_not_embed_center_metadata(self):
        sample = synthetic("dev-test", 1, "visible", x=26)
        for x,y in ((0,0),(26,422),(195,422),(389,843)):
            features = m223.descriptor(sample["rgb"], x,y)
            self.assertEqual((246,), features.shape)
            self.assertTrue(np.isfinite(features).all())
        a = m223.descriptor(sample["rgb"], 26,422)
        b = m223.descriptor(sample["rgb"], 195,422)
        self.assertGreater(float(np.linalg.norm(a-b)),0.1)
        self.assertEqual(m223.BINS,8)

    def test_absent_frame_calibration_does_not_treat_empty_patches_as_absence(self):
        frames=[synthetic("dev-a", j, "visible", x=160+20*j) for j in range(4)]
        with self.assertRaisesRegex(ValueError,"ABSENT"):
            m223.train(frames,grid=((100,422),(190,422),(320,422)),
                       negative_grid=((25,100),(200,100)))
        frames.extend(synthetic("dev-a", j+4, "absent") for j in range(3))
        model=m223.train(frames,grid=((100,422),(190,422),(320,422)),
                         negative_grid=((25,100),(200,100)))
        self.assertEqual(3,model.train_absent_frames)
        self.assertTrue(.5 <= model.absent_threshold <= 1.)
        self.assertEqual(4,model.train_positive_patches)
        self.assertGreaterEqual(model.train_negative_patches, 6)

    def test_unknown_never_counts_as_absent(self):
        f=[synthetic("dev-a", j, "visible") for j in range(4)]
        f.extend(synthetic("dev-a", j+5, "unknown") for j in range(5))
        with self.assertRaisesRegex(ValueError,"ABSENT"):
            m223.train(f,grid=((195,422),(55,200)),negative_grid=((55,200),))
        f.append(synthetic("dev-a", 99, "illegal"))
        with self.assertRaisesRegex(ValueError,"Invalid original owner state"):
            m223.train(f,grid=((195,422),),negative_grid=((55,200),))

    def test_run_split_excludes_exact_duplicate_sources(self):
        data={r:[synthetic(r,j,"visible",x=80+j*25) for j in range(3)]+
                [synthetic(r,j+3,"absent") for j in range(3)]
              for r in ("dev-l","dev-m","dev-n")}
        data["dev-m"][4]["sha"]=data["dev-l"][4]["sha"]
        folds={f["heldOut"]:f for f in m223.independent_folds(data)}
        self.assertEqual(["dev-l","dev-m","dev-n"], list(folds))
        self.assertGreaterEqual(folds["dev-l"]["duplicateSourceExcluded"],1)
        self.assertGreaterEqual(folds["dev-m"]["duplicateSourceExcluded"],1)
        self.assertTrue(all(all(x["sha"] not in {y["sha"] for y in data[f["heldOut"]]}
                                for x in f["fit"]) for f in folds.values()))

    def test_original_held_out_and_insufficient_grouping_rejected(self):
        with self.assertRaisesRegex(ValueError,"NOT_READY"):
            list(m223.independent_folds({"dev-l":[synthetic("dev-l",0,"absent")]}))
        with self.assertRaisesRegex(ValueError,"run-01"):
            list(m223.independent_folds({
                "dev-l":[synthetic("dev-l",0,"absent")],
                "dev-m":[synthetic("dev-m",0,"absent")],
                "run-01":[synthetic("run-01",0,"absent")]}))

    def test_model_is_deterministic_without_private_owner_inputs(self):
        samples=[synthetic("dev-a",j,"visible",x=100+j*20) for j in range(4)]
        samples.extend(synthetic("dev-a",j+4,"absent") for j in range(3))
        a=m223.train(samples,grid=((150,422),(300,400)),
                     negative_grid=((20,120),(210,180)))
        b=m223.train(samples,grid=((150,422),(300,400)),
                     negative_grid=((20,120),(210,180)))
        np.testing.assert_allclose(a.weights,b.weights,atol=0,rtol=0)
        self.assertEqual(a.absent_threshold,b.absent_threshold)


if __name__=="__main__":
    unittest.main()
