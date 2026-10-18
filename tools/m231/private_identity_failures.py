#!/usr/bin/env python3
"""M2.31: read-only/private causal identity model failure atlas; never tune model.

No training parameters are changed. All original owner-reviewed input and output
remain local; the code, tests and docs contain only artificial examples.
"""
import argparse
import json
import sys
from collections import Counter
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from tools.m230 import identity_study as model


def categorize(state, true_center, top, best_logit, threshold, true_logit=None):
    """Split model ranking from absent-calibration rejection without moving cutoff."""
    visible = state == 'visible'
    if not visible and state != 'absent':
        return None
    accepted = best_logit > threshold
    distance = (float(np.hypot(top[0]-true_center['x'], top[1]-true_center['y']))
                if visible else None)
    near = visible and distance <= model.LOCATION_TOLERANCE
    if not visible:
        kind = 'absent_false_candidate' if accepted else 'correctly_absent'
    elif accepted and near:
        kind = 'located'
    elif accepted:
        kind = 'wrong_location'
    else:
        kind = 'abstained_visible'
    return {'kind': kind, 'accepted': bool(accepted), 'rawTopNearRealPlayer': bool(near),
            'topToRealPixels': round(distance, 3) if distance is not None else None,
            'maxLogitMinusThreshold': round(best_logit-threshold, 5),
            'maxLogitMinusTruePatch': round(best_logit-true_logit, 5) if true_logit is not None else None}


def diagnose(by_run, output_dir, *, render_images=False):
    model.valid_samples(by_run)
    if output_dir.exists():
        raise ValueError('Private result destination exists; refuse to overwrite.')
    output_dir.mkdir(parents=True, exist_ok=False)
    rows = []
    for held in model.RUNS:
        test, fit, cal, _ = model.fold_partition(by_run, held)
        learner = model.fit_model(fit)
        threshold = model.fit_absent_threshold(cal, learner)
        for frame in test:
            if frame['state'] == 'unknown':
                continue
            best, (x, y) = model.image_max(frame, learner)
            center = frame['center']
            if center is None:
                true = None
            else:
                descriptor = model.descriptor(model.padded(frame['rgb']), center['x'], center['y'])
                true = float(((descriptor-learner['mean'])/learner['std'])@learner['weights']+learner['bias'])
            result = categorize(frame['state'], center, (x, y), best, threshold, true)
            rows.append({'run': held, 'step': frame['step'], 'originalSha256': frame['sha'],
                         'humanCenter': center, 'maxScoreCenter': {'x': x, 'y': y}, **result})
            if render_images and result['kind'] not in ('located', 'correctly_absent'):
                rgb = Image.fromarray(frame['rgb'])
                drawer = ImageDraw.Draw(rgb)
                if center:
                    xc, yc = center['x'], center['y']
                    drawer.ellipse((xc-10, yc-10, xc+10, yc+10), outline='#ff19dd', width=3)
                drawer.ellipse((x-11, y-11, x+11, y+11), outline='#ffbe22', width=3)
                rgb.save(output_dir/f'{held}-{frame["step"]:04d}.png')
    categories = Counter(row['kind'] for row in rows)
    by_source = {run: dict(Counter(row['kind'] for row in rows if row['run'] == run))
                 for run in model.RUNS}
    abstained = [r for r in rows if r['kind'] == 'abstained_visible']
    summary = {'schemaVersion': 1, 'status': 'PRIVATE_DEVELOPMENT_ONLY_NO_RETRAINING',
               'protocol': 'original frozen M2.30 whole-run-out (unchanged hyperparameters)',
               'samples': len(rows), 'categories': dict(categories), 'perRunCategories': by_source,
               'nearPlayerButAbstained': sum(x['rawTopNearRealPlayer'] for x in abstained),
               'abstainedVisible': len(abstained),
               'allSourceShaUnique': len({r['originalSha256'] for r in rows}),
               'interpretation': 'Descriptive already-reviewed developer errors; NEVER tune this same fold and claim unseen performance.',
               'sourceIsPrivate': True, 'modelOrGameplayChanged': False,
               'privateFrameRecords': rows}
    (output_dir/'private_m231_diagnostics.json').write_text(json.dumps(summary, indent=2)+'\n')
    return summary


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--original-archive', required=True, type=Path)
    p.add_argument('--labels-dir', required=True, type=Path)
    p.add_argument('--output-dir', required=True, type=Path)
    p.add_argument('--render-private-overlays', action='store_true')
    args = p.parse_args()
    data = model.load_private(args.original_archive, args.labels_dir)
    summary = diagnose(data, args.output_dir, render_images=args.render_private_overlays)
    print('PRIVATE M2.31:', summary['categories'], 'near true but rejected:',
          summary['nearPlayerButAbstained'], '/', summary['abstainedVisible'])
    print('No thresholds were changed. NEVER upload screenshots, owner coordinates or private results.')


if __name__ == '__main__':
    main()
