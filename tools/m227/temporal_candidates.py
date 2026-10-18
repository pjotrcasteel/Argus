#!/usr/bin/env python3
"""ARGUS M2.27: screenshot-only temporal hypotheses, NOT player detection.

No game-specific sprites, player coordinates, DOM, hidden telemetry, prior model
weights, prospective sources or private owner labels. Tracks may follow HUDs,
particles and scenery: NEVER use as live agent player state.
"""
from dataclasses import dataclass
from typing import Iterable
import numpy as np

FRAME_H = 844
FRAME_W = 390
DOWNSAMPLE = 4
MAX_TRANSLATION = 80
REGISTRATION_MIN_PEAK_RATIO = 1.08
MIN_CAMERA_SHIFT = 4
PROPOSAL_STRIDE = 16
PROPOSAL_RADIUS = 20
SUPPRESS_RADIUS = 32
MAX_PROPOSALS = 16
MAX_TRACK_JUMP = 110
MIN_APPEARANCE_COSINE = .65
MIN_NEW_PROPOSAL_CONTRAST = .03


@dataclass(frozen=True)
class Registration:
    dx: int
    dy: int
    peak_ratio: float
    reliable: bool


@dataclass(frozen=True)
class Proposal:
    x: int
    y: int
    novelty: float
    appearance: tuple[float, ...]


@dataclass(frozen=True)
class Track:
    track_id: int
    x: int
    y: int
    age: int
    last_step: int
    appearance: tuple[float, ...]
    last_novelty: float


def check_image(rgb):
    if not isinstance(rgb, np.ndarray) or rgb.shape != (FRAME_H, FRAME_W, 3) or rgb.dtype != np.uint8:
        raise ValueError("Expected an authentic 390x844 RGB screenshot.")


def luminance(rgb):
    check_image(rgb)
    return (.2126 * rgb[..., 0].astype(np.float32) +
            .7152 * rgb[..., 1].astype(np.float32) +
            .0722 * rgb[..., 2].astype(np.float32)) / 255.0


def registration(prev, current):
    """Estimate global scene displacement via FFT phase correlation, not identity."""
    a = luminance(prev)[:, :FRAME_W // DOWNSAMPLE * DOWNSAMPLE]
    b = luminance(current)[:, :FRAME_W // DOWNSAMPLE * DOWNSAMPLE]
    h, w = FRAME_H // DOWNSAMPLE, FRAME_W // DOWNSAMPLE
    a = a.reshape(h, DOWNSAMPLE, w, DOWNSAMPLE).mean(axis=(1, 3))
    b = b.reshape(h, DOWNSAMPLE, w, DOWNSAMPLE).mean(axis=(1, 3))
    a = np.hypot(np.roll(a, 1, axis=0) - a, np.roll(a, 1, axis=1) - a)
    b = np.hypot(np.roll(b, 1, axis=0) - b, np.roll(b, 1, axis=1) - b)
    window = np.hanning(h)[:, None] * np.hanning(w)[None, :]
    a = (a - a.mean()) * window
    b = (b - b.mean()) * window
    f, g = np.fft.rfft2(a), np.fft.rfft2(b)
    cross = g * np.conj(f)
    power = np.abs(cross)
    cross = np.divide(cross, power, out=np.zeros_like(cross), where=power > 1e-10)
    response = np.abs(np.fft.irfft2(cross, s=(h, w)))
    maximum = MAX_TRANSLATION // DOWNSAMPLE
    candidates = [(float(response[dy % h, dx % w]), dx, dy)
                  for dy in range(-maximum, maximum + 1)
                  for dx in range(-maximum, maximum + 1)]
    candidates.sort(reverse=True)
    peak, dx, dy = candidates[0]
    secondary = max((v for v, x, y in candidates if abs(x-dx) > 1 or abs(y-dy) > 1), default=0.)
    ratio = peak / max(secondary, 1e-12)
    reliable = bool(peak > 1e-7 and ratio >= REGISTRATION_MIN_PEAK_RATIO)
    return Registration(dx * DOWNSAMPLE if reliable else 0,
                        dy * DOWNSAMPLE if reliable else 0,
                        round(float(ratio), 4), reliable)


def shift_with_valid(image, dx, dy):
    """Align earlier image to current image coordinates; never circular-wrap."""
    h, w = image.shape[:2]
    out = np.zeros_like(image)
    valid = np.zeros((h, w), dtype=bool)
    y0, y1 = max(0, dy), min(h, h + dy)
    x0, x1 = max(0, dx), min(w, w + dx)
    if y0 < y1 and x0 < x1:
        out[y0:y1, x0:x1] = image[y0-dy:y1-dy, x0-dx:x1-dx]
        valid[y0:y1, x0:x1] = True
    return out, valid


def temporal_novelty(prev, current, reg):
    """Scene-shifted residual minus fixed-screen residual: FOREGROUND-LIKE only.

    Static HUD and stationary character can BOTH have high responses. No identity
    or player classification follows from novelty. Abstain if registration fails.
    """
    if not isinstance(reg, Registration):
        raise TypeError("Registration provenance required.")
    a, b = luminance(prev), luminance(current)
    if not reg.reliable or abs(reg.dx) + abs(reg.dy) < MIN_CAMERA_SHIFT:
        return np.zeros((FRAME_H, FRAME_W), np.float32)
    aligned, valid = shift_with_valid(a, reg.dx, reg.dy)
    saliency = np.maximum(np.abs(b-aligned) - np.abs(b-a), 0.)
    saliency[~valid] = 0.
    return saliency.astype(np.float32)


def signature(rgb, x, y):
    """Local RGB/shape appearance, not player identity nor x/y model features."""
    check_image(rgb)
    if type(x) is not int or type(y) is not int or not (0 <= x < FRAME_W and 0 <= y < FRAME_H):
        raise ValueError("Invalid image candidate position.")
    pad = np.pad(rgb, ((PROPOSAL_RADIUS, PROPOSAL_RADIUS),
                       (PROPOSAL_RADIUS, PROPOSAL_RADIUS), (0, 0)), mode="edge")
    patch = pad[y:y+40, x:x+40].astype(np.float32) / 255.
    cells = patch.reshape(8, 5, 8, 5, 3).mean(axis=(1, 3))
    lum = cells @ [.2126, .7152, .0722]
    z = (lum - lum.mean()).ravel()
    z /= max(float(np.linalg.norm(z)), 1e-6)
    return tuple(float(t) for t in np.concatenate((z, cells.mean(axis=(0, 1)) - patch.mean())))


def proposals(current, saliency, *, limit=MAX_PROPOSALS):
    check_image(current)
    if saliency.shape != (FRAME_H, FRAME_W):
        raise ValueError("Frame saliency geometry changed.")
    if not 1 <= limit <= MAX_PROPOSALS:
        raise ValueError("Bounded proposal count required.")
    grid = []
    for y in range(PROPOSAL_RADIUS, FRAME_H-PROPOSAL_RADIUS, PROPOSAL_STRIDE):
        for x in range(PROPOSAL_RADIUS, FRAME_W-PROPOSAL_RADIUS, PROPOSAL_STRIDE):
            response = float(saliency[y-12:y+12, x-12:x+12].mean())
            if response >= MIN_NEW_PROPOSAL_CONTRAST:
                grid.append((response, x, y))
    grid.sort(key=lambda q: (-q[0], q[2], q[1]))
    found = []
    for novelty, x, y in grid:
        if all((x-p.x)**2 + (y-p.y)**2 >= SUPPRESS_RADIUS**2 for p in found):
            found.append(Proposal(x, y, round(novelty, 6), signature(current, x, y)))
            if len(found) >= limit:
                break
    return found


def normalized_cosine(a, b):
    x, y = np.asarray(a, dtype=np.float64), np.asarray(b, dtype=np.float64)
    if x.shape != y.shape or x.ndim != 1:
        raise ValueError("Cannot compare mismatched image appearances.")
    return float(x @ y / max(np.linalg.norm(x)*np.linalg.norm(y), 1e-12))


def update_tracks(prior: Iterable[Track], current: list[Proposal], step: int):
    """Causal appearance/position association; HUD may also build tracks."""
    if type(step) is not int or step < 0:
        raise ValueError("Chronological original screenshot step required.")
    previous = list(prior)
    if any(t.last_step >= step for t in previous):
        raise ValueError("Out-of-order or repeated screenshots must not build tracks.")
    used, result = set(), []
    next_id = max((t.track_id for t in previous), default=-1) + 1
    for p in current:
        matches = []
        for old in previous:
            if old.track_id in used:
                continue
            distance = float(np.hypot(p.x-old.x, p.y-old.y))
            if distance > MAX_TRACK_JUMP:
                continue
            similarity = normalized_cosine(old.appearance, p.appearance)
            if similarity >= MIN_APPEARANCE_COSINE:
                matches.append((similarity - .25*distance/MAX_TRACK_JUMP, -old.track_id, old))
        if matches:
            match = max(matches)[2]
            used.add(match.track_id)
            result.append(Track(match.track_id, p.x, p.y, match.age+1, step,
                                p.appearance, p.novelty))
        else:
            result.append(Track(next_id, p.x, p.y, 1, step, p.appearance, p.novelty))
            next_id += 1
    return result


def analyze_pair(previous, current, step, prior_tracks=()):
    """Source-only report: NEVER a prediction that player is visibly present."""
    reg = registration(previous, current)
    saliency = temporal_novelty(previous, current, reg)
    found = proposals(current, saliency)
    tracks = update_tracks(prior_tracks, found, step)
    return {"schemaVersion": 1, "status": "SOURCE_ONLY_CANDIDATES_NOT_PLAYERS",
            "step": step, "registration": {"dx": reg.dx, "dy": reg.dy,
                                           "peakRatio": reg.peak_ratio,
                                           "reliable": reg.reliable},
            "proposals": [{"x": p.x, "y": p.y, "novelty": p.novelty} for p in found],
            "tracks": [{"id": t.track_id, "x": t.x, "y": t.y, "age": t.age} for t in tracks]}, tracks
