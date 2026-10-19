# M2.42 — Owner-only local iPhone review kit

## What changed

M2.41 already validates original owner labels and refuses wrong screenshot
SHA, wrong run, invented player centers, duplicate contradiction and UNKNOWN
being misreported as ABSENT. But asking an iPhone owner to run a static
website or publish unreviewed original PNGs publicly is too cumbersome
and risks leaking private research data.

M2.42 delivers a **self-contained read-only local mobile review site**.
The site serves the existing frozen **M2.38** review UI unchanged:
54 screenshot observations from three real independent WyrmForge sessions,
53 distinct original PNG SHA digests, and 54 genuine immediate predecessor
frames, with **zero human labels** and no learned model or prediction.

`tools/m242/package_private_review.py` first runs the complete M2.40
source SHA/original-frame/trace verification. Only afterwards may it pack
the original-only review ZIP. The original full three-session trace ZIP
is **never** bundled into the lightweight owner kit. No image, label,
game input or experiment data is sent to GitHub Pages or remote servers.

The independent, stdlib-only `private_review_server.py` is included
in the generated owner kit. There is no need for Git, .NET, Pillow,
Playwright, game source, internet service or subscription to use it.

## Create the immutable private owner kit (maintainer)

Download and save the two **original private GitHub Action artifacts** from
[M2.38 original run 39242924416](https://github.com/pjotrcasteel/Argus/actions/runs/39242924416)
before they expire (2026-10-26), and extract each locally:

- `argus-m238-real-external-source-no-owner-labels` into `/private/m238/source`;
- `argus-m238-private-original-review-not-evaluated` into `/private/m238/review`.

With Python 3.12+, the repo checked out and free `Pillow` installed
(`python -m pip install "Pillow>=11,<13"`):

```bash
PYTHONPATH=. python -m tools.m242.package_private_review \
  --sources /private/m238/source \
  --review /private/m238/review \
  --output /private/m238/argus-mobile-review-kit.zip
```

This writes a ZIP **outside the public repo** and refuses mismatched
original byte SHA, wrong frozen selection, source trace tampering,
added labels and unknown files.

## Use the mobile review kit (owner)

Unzip `argus-mobile-review-kit.zip` on your **own computer**.

```bash
cd argus-m238-private-review
python private_review_server.py
```

The command prints a randomized one-session local URL. Opening it in a
browser on the same computer gives the original reviewer. The default
server binds **only `127.0.0.1`**. No downloads to or uploads from a
cloud service occur; the site is intentionally read-only.

For an iPhone **on the same trusted home Wi-Fi as your computer**, find
the computer's specific IPv4 address and explicitly opt in:

```bash
# Replace this example with YOUR computer's real private Wi-Fi address.
python private_review_server.py --lan --host 192.168.1.23
```

Enter the printed temporary URL in Safari. The initial token only
creates a same-site HttpOnly browser cookie, then redirects to the clean
home page. A new server run generates a different random token. Access
without it is denied; file listings, original trace files, manifest,
unused preceding source screenshots and mutation requests are not served.

**Security qualification:** this is HTTP on your own LAN, **not**
end-to-end TLS encryption. Avoid guest/public/shared or compromised
Wi-Fi, do not forward the URL, expose ports, or bind `0.0.0.0`.
LAN traffic could theoretically be observed by another machine with
network-sniffing capability; default localhost is safest.
This is not a hosting solution for anonymous/public use.

Use VISIBLE only for the center of the *playable* mage. ABSENT means
no playable mage in the whole screenshot. UNKNOWN is the right answer
when uncertain or obscured. Each of the three review pages contains
18 targets; data stays in that browser's `localStorage` until exporting.
Before changing browsers, clearing Safari storage or closing permanently,
click **Exporteer eigen JSON** on each run; keep the resulting three
files privately:

- `m238-a-player-points.json`
- `m238-b-player-points.json`
- `m238-c-player-points.json`

Supply the three genuine owner exports back to ARGUS for the private
M2.41 original-byte and consistency audit. Do not hand-edit frame
SHA/step order. This is **human reference collection**, not proof of
player classification or model performance.

## Independent evidence and next boundary

The dedicated M2.42 GitHub Actions suite uses **synthetic original-sized
RGB PNGs and fictional labels only**. It runs a full browser-access test
against the actual authenticated Python localhost server using mobile
Chromium, verifies touch geometry and JSON round-trip, and refuses
wrong bind addresses, missing authorization, mutated methods, unknown
paths, altered originals and accidentally bundled trace/owner files.

To validate the authentic owner kit, run M2.42 packer locally with
both original preserved source artifacts; never commit the ZIP or
upload it to public CI/Pages. A successful bundle + synthetically
tested reviewer is **not** a human-reviewed result. Existing private
M2.38 observations remain `NOT_EVALUATED` until real owner exports.

Once the owner supplies those three JSON exports, use M2.41's private
admission and report coverage. Do not train on M2.38 frames and then
count them as disjoint prospective validation. M2.37 still requires
genuine fit/whole-ABSENT calibration from separate source runs and
complete chronological original-frame replay. O/P/Q and old M2.4
run-01 remain sealed; ARGUS live steering is unchanged.
