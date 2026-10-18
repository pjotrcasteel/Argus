#!/usr/bin/env python3
"""M2.30: learned local appearance, explicit absence, fully offline and PRIVATE.

Research only. M2.27 temporal novelty is NOT a prerequisite, so its documented
failure cannot silently prevent actual player-looking pixels from being scanned.
No x/y, screen center, game states or profile color keys enter descriptors.
Do not activate in live ARGUS or use sealed O/P/Q.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path
from zipfile import ZipFile

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
from tools.m229.evaluate_temporal_references import (
    FROZEN_ARCHIVE_SHA256, FROZEN_STUDY_SEED, RUNS, image_bytes, evaluate as verify_source,
)

GRID_X = tuple(sorted(set(range(0, 390, 16)) | {389}))
GRID_Y = tuple(sorted(set(range(0, 844, 16)) | {843}))
GRID = tuple((x, y) for y in GRID_Y for x in GRID_X)
NEGATIVE_X = (0, 64, 128, 192, 256, 320, 389)
NEGATIVE_Y = (0, 112, 224, 336, 448, 560, 672, 784, 843)
NEGATIVE_GRID = tuple((x, y) for y in NEGATIVE_Y for x in NEGATIVE_X)
PATCH = 48
EXCLUDE_RADIUS = 72
MIN_VISIBLE_FIT = 8
MIN_ABSENT_FIT = 1
MIN_ABSENT_CAL = 3
EPOCHS = 350
LEARNING_RATE = .12
L2 = .15
ABSENT_MARGIN = .05
LOCATION_TOLERANCE = 35
MIN_VISIBLE_RECALL = .50
MAX_ABSENT_FP_RATE = .25
MODEL_ID = 'M2.30-RGB-shape-contrast-whole-run-out-v1'


def demand(yes, reason):
    if not yes:
        raise ValueError(reason)


def canonical(obj):
    return json.dumps(obj, sort_keys=True, separators=(',', ':'), allow_nan=False)


def sha(text):
    return hashlib.sha256(text.encode()).hexdigest()


def padded(rgb):
    demand(isinstance(rgb, np.ndarray) and rgb.shape == (844, 390, 3) and rgb.dtype == np.uint8,
           'Invalid original 390x844 RGB screenshot.')
    return np.pad(rgb, ((24, 24), (24, 24), (0, 0)), mode='edge')


def descriptor(prepared, x, y):
    demand(type(x) is int and type(y) is int and 0 <= x < 390 and 0 <= y < 844,
           'Image sampling center outside original screenshot.')
    patch = prepared[y:y+PATCH, x:x+PATCH].astype(np.float32)/255.
    cells = patch.reshape(12, 4, 12, 4, 3).mean(axis=(1, 3))
    lum = cells @ np.asarray([.2126, .7152, .0722], dtype=np.float32)
    lum = (lum-lum.mean())/(lum.std()+.15)
    rgb6 = cells.reshape(6, 2, 6, 2, 3).mean(axis=(1, 3))
    gx = np.abs(np.diff(lum, axis=1, prepend=lum[:, :1]))
    gy = np.abs(np.diff(lum, axis=0, prepend=lum[:1, :]))
    edge6 = (gx+gy).reshape(6, 2, 6, 2).mean(axis=(1, 3))
    return np.concatenate((lum.ravel(), rgb6.ravel(), edge6.ravel())).astype(np.float32)


def features(rgb, points):
    prepared = padded(rgb)
    return np.stack([descriptor(prepared, int(x), int(y)) for x, y in points])


def valid_samples(by_run):
    demand(set(by_run) == set(RUNS), 'Only three private development runs accepted.')
    for run, frames in by_run.items():
        demand(len(frames) == 18, 'Each frozen original run requires 18 reviewed frames.')
        keys = set()
        for f in frames:
            demand(f['run'] == run and f['state'] in ('visible', 'absent', 'unknown') and
                   len(f['sha']) == 64, 'Private verified run or source SHA missing.')
            if f['sha'] in keys:
                continue
            keys.add(f['sha'])
            padded(f['rgb'])
            if f['state'] == 'visible':
                c = f['center']
                demand(c is not None and type(c['x']) is int and type(c['y']) is int and
                       0 <= c['x'] < 390 and 0 <= c['y'] < 844, 'Invalid human location.')
            else:
                demand(f['center'] is None, 'Absent / unknown cannot carry coordinate.')


def dedup_source(frames):
    found = {}
    for f in frames:
        if f['sha'] in found:
            original = found[f['sha']]
            demand((original['state'], original['center']) == (f['state'], f['center']),
                   'Contradictory identical PNG truth.')
        else:
            found[f['sha']] = f
    return list(found.values())


def fold_partition(by_run, held):
    """No held-run image may enter fit OR absence calibration, even across runs."""
    demand(held in RUNS, 'Unknown development run.')
    test = dedup_source(by_run[held])
    blocked = {f['sha'] for f in by_run[held]}
    other = [f for run in RUNS if run != held for f in by_run[run] if f['sha'] not in blocked]
    other = dedup_source(other)
    fit, cal = [], []
    for run in RUNS:
        if run == held:
            continue
        subset = [f for f in other if f['run'] == run]
        absent = [f for f in subset if f['state'] == 'absent']
        calibrate = {f['sha'] for i, f in enumerate(absent) if i % 2 == 0}
        fit.extend(f for f in subset if f['sha'] not in calibrate and f['state'] != 'unknown')
        cal.extend(f for f in subset if f['sha'] in calibrate)
    demand(not ({f['sha'] for f in fit} & {f['sha'] for f in cal}) and
           not (blocked & ({f['sha'] for f in fit} | {f['sha'] for f in cal})),
           'Original screenshot SHA leakage between train, cal and held-run.')
    return test, fit, cal, len(other)+sum(f['sha'] in blocked for run in RUNS if run != held for f in by_run[run])


def fit_model(fit):
    positives, negatives = [], []
    for f in fit:
        center = f['center']
        if center is not None:
            positives.append(descriptor(padded(f['rgb']), center['x'], center['y']))
        negatives.extend(features(f['rgb'], [
            (x, y) for x, y in NEGATIVE_GRID
            if center is None or (x-center['x'])**2+(y-center['y'])**2 >= EXCLUDE_RADIUS**2]))
    demand(len(positives) >= MIN_VISIBLE_FIT and len(negatives) >= 3 and
           sum(f['state'] == 'absent' for f in fit) >= MIN_ABSENT_FIT,
           'NOT_READY: insufficient verified whole-frame training controls.')
    X = np.asarray(positives+negatives, dtype=np.float64)
    y = np.asarray([1.]*len(positives)+[0.]*len(negatives), dtype=np.float64)
    mu = X.mean(axis=0)
    std = np.maximum(X.std(axis=0), .005)
    Z = (X-mu)/std
    w = np.zeros(Z.shape[1], dtype=np.float64)
    bias = 0.
    balanced = np.where(y == 1, .5/len(positives), .5/len(negatives))
    for _ in range(EPOCHS):
        logits = np.clip(Z@w+bias, -35, 35)
        grad = (1/(1+np.exp(-logits))-y)*balanced
        w -= LEARNING_RATE*(Z.T@grad+L2*w)
        bias -= LEARNING_RATE*float(grad.sum())
    demand(bool(np.isfinite(w).all()) and math.isfinite(bias), 'Nonfinite fitted appearance.')
    return {'mean': mu, 'std': std, 'weights': w, 'bias': bias,
            'fitPositiveImages': len(positives), 'fitNegativePatches': len(negatives)}


def image_max(frame, model, grid=GRID):
    feats = features(frame['rgb'], grid)
    scores = ((feats-model['mean'])/model['std'])@model['weights']+model['bias']
    demand(bool(np.isfinite(scores).all()), 'Invalid image-wide fit logits.')
    idx = int(np.argmax(scores))
    return float(scores[idx]), tuple(grid[idx])


def fit_absent_threshold(cal, model, grid=GRID):
    demand(len(cal) >= MIN_ABSENT_CAL and all(f['state'] == 'absent' for f in cal) and
           len({f['sha'] for f in cal}) == len(cal),
           'NOT_READY: three distinct human-ABSENT calibration screenshots required.')
    scores = [image_max(f, model, grid)[0] for f in cal]
    return max(scores)+ABSENT_MARGIN


def decide(frame, model, threshold, grid=GRID):
    score, center = image_max(frame, model, grid)
    yes = score > threshold
    return {'present': bool(yes), 'center': center if yes else None, 'topScore': score}


def confusion(frames, predictions):
    out = {'visible': 0, 'absent': 0, 'correctWithin35': 0, 'falsePresence': 0,
           'missedVisible': 0, 'presentButWrongLocation': 0}
    for frame, pred in zip(frames, predictions):
        if frame['state'] == 'unknown':
            continue
        if frame['state'] == 'absent':
            out['absent'] += 1
            out['falsePresence'] += int(pred['present'])
        else:
            out['visible'] += 1
            if not pred['present']:
                out['missedVisible'] += 1
            else:
                c = frame['center']
                x, y = pred['center']
                if (x-c['x'])**2+(y-c['y'])**2 <= LOCATION_TOLERANCE**2:
                    out['correctWithin35'] += 1
                else:
                    out['presentButWrongLocation'] += 1
    return out


def evaluate_development(by_run, grid=GRID):
    valid_samples(by_run)
    folds = []
    for held in RUNS:
        test, fit, cal, _ = fold_partition(by_run, held)
        model = fit_model(fit)
        threshold = fit_absent_threshold(cal, model, grid)
        pred = [decide(f, model, threshold, grid) for f in test]
        metrics = confusion(test, pred)
        center = confusion(test, [{'present': True, 'center': (195, 422)} for _ in test])
        enough = metrics['visible'] >= 8 and metrics['absent'] >= 2
        go = (enough and metrics['correctWithin35']/metrics['visible'] >= MIN_VISIBLE_RECALL and
              metrics['falsePresence']/metrics['absent'] <= MAX_ABSENT_FP_RATE)
        model_private = {key: (value.tolist() if isinstance(value, np.ndarray) else value)
                         for key, value in model.items()}
        model_hash = sha(canonical({'model': model_private, 'threshold': threshold,
                                    'fitSha': sorted(f['sha'] for f in fit),
                                    'calSha': sorted(f['sha'] for f in cal)}))
        folds.append({'heldRun': held, 'fitRuns': [r for r in RUNS if r != held],
                      'fitVisible': model['fitPositiveImages'],
                      'fitNegativePatches': model['fitNegativePatches'],
                      'calAbsent': len(cal), 'calibrationMaxLogitPlusMargin': threshold,
                      'heldOutUniqueSourceImages': len(test), 'metrics': metrics,
                      'centerAlwaysPresent': center, 'go': go,
                      'privateModelHash': model_hash})
    return {'schemaVersion': 1, 'method': MODEL_ID, 'status': 'EXPLORATORY_DEVELOPMENT_ONLY',
            'wholeRunOutFolds': folds, 'allRunGo': all(x['go'] for x in folds),
            'next': 'Do not inspect sealed O/P/Q or change live agent on development-only evidence.',
            'originalProspectiveOpened': False, 'liveAgentChanged': False}


def load_private(archive, labels_dir):
    # Reuse exact approved original archive+pair+review validation; no prospective sources.
    verify_source(archive, labels_dir)
    with ZipFile(archive) as z:
        manifest = json.loads(z.read('study-manifest.json'))
        by_run = {}
        for source in manifest['runs']:
            run = source['runId']
            labels = json.loads((labels_dir/f'm227-{run}-player-points.json').read_text())
            frames = []
            for image, review in zip(source['images'], labels['frames']):
                rgb = image_bytes(z.read(f"annoteren/{run}/{image['file']}"), image['sha256'])
                player = review['player']
                frames.append({'run': run, 'state': player['state'], 'center': player['center'],
                               'sha': image['sha256'], 'rgb': rgb, 'step': image['step']})
            by_run[run] = frames
    return by_run


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--original-archive', required=True, type=Path)
    p.add_argument('--labels-dir', required=True, type=Path)
    p.add_argument('--output', required=True, type=Path)
    a = p.parse_args()
    demand(not a.output.exists(), 'Refuse to overwrite a private research report.')
    result = evaluate_development(load_private(a.original_archive, a.labels_dir))
    a.output.write_text(json.dumps(result, indent=2)+'\n')
    for r in result['wholeRunOutFolds']:
        m = r['metrics']
        print(r['heldRun'], m['correctWithin35'], '/', m['visible'],
              'falsePresence', m['falsePresence'], '/', m['absent'],
              'go', r['go'], flush=True)
    print('PRIVATE DEVELOPMENT ONLY —', result['allRunGo'],
          'NEVER upload labels, models or per-frame detail.')


if __name__ == '__main__':
    main()
