#!/usr/bin/env python3
"""M2.35: screenshot-only CAUSAL temporal identity hypotheses, never live player state.

A single image-wide shape logit cannot establish player identity. This shadow
research module links competing original-image hypotheses through consecutive
frames, refuses repeated or skipped frames, and NEVER emits a verified player
position. No labels, privileged game data, assets, IDs, DOM, OCR or game input.
"""
from dataclasses import dataclass
import hashlib
import math

import numpy as np

from tools.m230 import identity_study as appearance

MODEL_ID = "m235-causal-multi-hypothesis-not-player-identity-v1"
MAX_CANDIDATES = 12
SUPPRESSION_PX = 32
MAX_STEP_DISPLACEMENT_PX = 120
MIN_COSINE = .86
MIN_SCREEN_PIXEL_CHANGE = .012
REQUIRED_CONSECUTIVE_FRAMES = 3
REQUIRED_CHANGED_TRANSITIONS = 2
MIN_IDENTITY_MARGIN = .40


def require(ok, explanation):
    if not ok:
        raise ValueError(explanation)


def frame_hash(image):
    require(isinstance(image, np.ndarray) and image.shape == (844, 390, 3) and
            image.dtype == np.uint8, "Only 390x844 original-size RGB screenshots.")
    return hashlib.sha256(image.tobytes()).hexdigest()


def cosine(a, b):
    one, other = np.asarray(a, np.float64), np.asarray(b, np.float64)
    require(one.ndim == 1 and one.shape == other.shape and one.size > 0 and
            np.isfinite(one).all() and np.isfinite(other).all(),
            "Original-image appearance vectors must have identical finite shape.")
    denom = float(np.linalg.norm(one) * np.linalg.norm(other))
    return float(one @ other / denom) if denom > 1e-12 else 0.


@dataclass(frozen=True)
class Candidate:
    x: int
    y: int
    appearance: tuple[float, ...]
    image_logit: float
    raw_change: float

    def __post_init__(self):
        require(type(self.x) is int and type(self.y) is int and
                0 <= self.x < 390 and 0 <= self.y < 844,
                "Candidate must lie inside original 390x844 frame.")
        require(len(self.appearance) > 0 and all(math.isfinite(x) for x in self.appearance) and
                math.isfinite(self.image_logit) and
                math.isfinite(self.raw_change) and 0 <= self.raw_change <= 1.,
                "Malformed finite appearance / change evidence.")


@dataclass(frozen=True)
class Track:
    track_id: int
    candidate: Candidate
    age: int
    changed_transitions: int
    last_step: int
    match_cosine: float


def _raw_patch(rgb, x, y):
    # Edge repetition is necessary for x=0 and x=389; no edge-only exclusion.
    padded = np.pad(rgb, ((24, 24), (24, 24), (0, 0)), mode="edge")
    return padded[y:y+48, x:x+48]


def original_image_candidates(prior_rgb, current_rgb, fit_model, *,
                              grid=appearance.GRID, limit=MAX_CANDIDATES):
    """Image-wide ORIGINAL pixels, fit-only model; no previous player oracle.

    The change feature is raw image appearance at the same screen coordinate,
    NOT a claim of camera-compensated object motion or player presence.
    Static HUD and a truly motionless player may both be rejected.
    """
    frame_hash(prior_rgb)
    frame_hash(current_rgb)
    require(1 <= limit <= MAX_CANDIDATES, "Bounded candidate count required.")
    require(len(grid) >= 3 and len(grid) == len(set(grid)),
            "Source-only original pixel candidate grid must have distinct points.")
    vectors = appearance.features(current_rgb, grid)
    mean, std, weight = (np.asarray(fit_model[name], np.float64)
                         for name in ("mean", "std", "weights"))
    require(vectors.shape[1:] == mean.shape == std.shape == weight.shape and
            np.isfinite(mean).all() and np.isfinite(std).all() and
            np.isfinite(weight).all() and (std > 0).all() and
            math.isfinite(float(fit_model["bias"])),
            "Fitted appearance model is invalid/incompatible.")
    logits = ((vectors.astype(np.float64)-mean)/std)@weight+float(fit_model["bias"])
    require(bool(np.isfinite(logits).all()), "Invalid image logits.")
    order = sorted(range(len(grid)), key=lambda i: (-logits[i], grid[i][1], grid[i][0]))
    previous = np.pad(prior_rgb, ((24, 24), (24, 24), (0, 0)), mode="edge")
    current = np.pad(current_rgb, ((24, 24), (24, 24), (0, 0)), mode="edge")
    found = []
    for i in order:
        x, y = (int(t) for t in grid[i])
        require(0 <= x < 390 and 0 <= y < 844, "Original pixel grid is out of bounds.")
        if any((x-c.x)**2 + (y-c.y)**2 < SUPPRESSION_PX**2 for c in found):
            continue
        changed = float(np.abs(current[y:y+48, x:x+48].astype(np.float32) -
                               previous[y:y+48, x:x+48].astype(np.float32)).mean()/255.)
        found.append(Candidate(x, y, tuple(float(v) for v in vectors[i]),
                               float(logits[i]), changed))
        if len(found) == limit:
            break
    return found


class CausalIdentityResearch:
    """Tracks ambiguous source-only hypotheses, never a recognized player.

    Track IDs are local to a single independently captured game session. A new
    run explicitly resets them; gaps, malformed steps or identical frames
    never extend old evidence. No true player point enters an update.
    """

    def __init__(self):
        self.run = None
        self.step = None
        self.source_sha = None
        self.tracks = ()
        self.next_id = 0

    def reset(self, run):
        require(type(run) is str and run and len(run) < 128,
                "Whole original source run identity required.")
        self.run, self.step, self.source_sha = run, None, None
        self.tracks, self.next_id = (), 0

    def update(self, run, step, source_sha, candidates):
        require(type(run) is str and run and type(step) is int and step >= 0 and
                type(source_sha) is str and len(source_sha) == 64 and
                all(ch in "0123456789abcdef" for ch in source_sha),
                "Original-image chronological step/run/SHA required.")
        require(type(candidates) in (tuple, list) and 0 <= len(candidates) <= MAX_CANDIDATES and
                all(isinstance(c, Candidate) for c in candidates),
                "Bounded original-image candidate evidence required.")
        require(len(set((c.x, c.y) for c in candidates)) == len(candidates),
                "Duplicate same-image candidate positions not independent.")
        if run != self.run:
            self.reset(run)
        require(self.step is None or step > self.step,
                "Repeated/out-of-order source frames invalidate causal research.")
        linked = (self.step is not None and step == self.step + 1 and
                  source_sha != self.source_sha)
        old = self.tracks if linked else ()
        # Greedy maximum first, globally, prevents one previous object being
        # assigned to multiple competitors; no engine/player labels involved.
        edges = []
        for i, now in enumerate(candidates):
            for track in old:
                distance = float(math.hypot(now.x-track.candidate.x,
                                            now.y-track.candidate.y))
                if distance > MAX_STEP_DISPLACEMENT_PX:
                    continue
                sim = cosine(track.candidate.appearance, now.appearance)
                if sim >= MIN_COSINE:
                    priority = sim - .15*distance/MAX_STEP_DISPLACEMENT_PX
                    edges.append((-priority, -sim, distance, track.track_id, i))
        edges.sort()
        used_old, used_new, associations = set(), set(), {}
        for _, neg_sim, _, track_id, index in edges:
            if track_id in used_old or index in used_new:
                continue
            associations[index] = (track_id, -neg_sim)
            used_old.add(track_id)
            used_new.add(index)
        by_id = {track.track_id: track for track in old}
        new_tracks = []
        for index, candidate in enumerate(candidates):
            if index in associations:
                identity, similarity = associations[index]
                prior = by_id[identity]
                changed = prior.changed_transitions + int(
                    candidate.raw_change >= MIN_SCREEN_PIXEL_CHANGE and
                    prior.candidate.raw_change >= MIN_SCREEN_PIXEL_CHANGE)
                new_tracks.append(Track(identity, candidate, prior.age+1,
                                        changed, step, similarity))
            else:
                new_tracks.append(Track(self.next_id, candidate, 1, 0, step, 0.))
                self.next_id += 1
        # Identity evidence stays a hypothesis even if it persists: a monster
        # or moving UI effect can also produce an apparently coherent track.
        self.tracks = tuple(new_tracks)
        self.step, self.source_sha = step, source_sha
        mature = [t for t in new_tracks if
                  t.age >= REQUIRED_CONSECUTIVE_FRAMES and
                  t.changed_transitions >= REQUIRED_CHANGED_TRANSITIONS]
        # Contrast based on *current fit-model evidence*, not owner point.
        mature.sort(key=lambda t: (-t.candidate.image_logit, t.track_id))
        top = mature[0] if mature else None
        margin = (top.candidate.image_logit-mature[1].candidate.image_logit
                  if len(mature) >= 2 else float("inf"))
        ambiguous = (len(mature) >= 2 and margin < MIN_IDENTITY_MARGIN)
        status = ("INSUFFICIENT_CAUSAL_IDENTITY_EVIDENCE" if not mature else
                  "MULTIPLE_LOOKALIKE_HYPOTHESES_ABSTAIN" if ambiguous else
                  "UNVERIFIED_SOURCE_ONLY_IDENTITY_HYPOTHESIS")
        return {
            "schemaVersion":1, "hypothesis":MODEL_ID, "run":run, "step":step,
            "status":status, "playerPresent":None, "verifiedPlayerCenter":None,
            "eligibleIdentityTracks":len(mature),
            "candidate":None if top is None or ambiguous else
                {"x":top.candidate.x,"y":top.candidate.y,"trackId":top.track_id,
                 "age":top.age,"changedTransitions":top.changed_transitions},
            "consecutiveOriginalFrames":bool(linked),
            "noPrivilegedTruthAccess":True, "liveAgentChanged":False}
