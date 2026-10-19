#!/usr/bin/env python3
"""M2.42 stdlib-only local M2.38 blind-review server. No uploads or telemetry.

This file is copied UNCHANGED into private reviewer archives. Keep it fully
standalone: recipients do not need .NET, Pillow, git, GitHub or ARGUS source.
"""
import argparse
import hashlib
import ipaddress
import json
import re
import secrets
import struct
from functools import partial
from http import HTTPStatus
from http.cookies import SimpleCookie
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlsplit

RUNS = ("m238-a", "m238-b", "m238-c")
SEED = "argus-m238-post-wyrmforge-121-20261018-original-v1"
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"


def demand(value, explanation):
    if not value:
        raise ValueError(explanation)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def file_bytes(root, name):
    path = root / name
    demand(path.is_file() and not path.is_symlink() and path.resolve().is_relative_to(root.resolve()),
           "Unsafe or missing original reviewer file.")
    return path.read_bytes()


def source_index(root: Path):
    """Fail closed before serving any original image (no dependency on traces)."""
    root = root.resolve()
    manifest = json.loads(file_bytes(root, "study-manifest.json"))
    demand(manifest.get("schemaVersion") == 1 and
           manifest.get("study") == "M2.38_NEW_EXTERNAL_DEVELOPMENT" and
           manifest.get("researchStatus") == "NOT_EVALUATED" and
           manifest.get("sourceSeed") == SEED and
           manifest.get("runIds") == list(RUNS) and
           manifest.get("originalTargetCount") == 54 and
           manifest.get("originalImmediatePredecessorCount") == 54 and
           manifest.get("ownerGroundTruth") == "NONE" and
           manifest.get("publicPublicationAuthorized") is False and
           manifest.get("prospectiveSealedSourcesAccessed") is False,
           "This is not the original frozen PRIVATE M2.38 review.")

    records = manifest.get("runs")
    demand(type(records) is list and len(records) == 3 and
           [r.get("runId") for r in records] == list(RUNS),
           "Frozen independent reviewer runs changed.")
    allowed = {"index.html"}
    index = file_bytes(root, "index.html").decode()
    demand(all(f"annoteren/{run}/" in index for run in RUNS) and "fetch(" not in index,
           "Unexpected private review index content.")
    seen = set()
    for run, record in zip(RUNS, records):
        html_path = f"annoteren/{run}/index.html"
        html = file_bytes(root, html_path).decode()
        demand(run in html and "/^m238-[abc]$/" in html and
               "fetch(" not in html and "playerX" not in html and
               "threatLocations" not in html and "verifiedPlayerCenter" not in html,
               "Private reviewer script mismatch or unwanted source access.")
        allowed.add(html_path)
        shots = record.get("images")
        context = record.get("immediatePriorOriginalFrames")
        demand(type(shots) is list and type(context) is list and
               len(shots) == len(context) == 18, "Frozen source sample count changed.")
        prev_step = -1
        for shot, predecessor in zip(shots, context):
            step = shot.get("step")
            demand(type(step) is int and step > prev_step and step > 0 and
                   shot.get("file") == f"frame-{step:04d}.png" and
                   predecessor.get("targetStep") == step and
                   predecessor.get("priorStep") == step - 1 and
                   predecessor.get("file") == f"context/{run}/prev-for-{step:04d}.png",
                   "Review target order or predecessor reference changed.")
            prev_step = step
            for path, expected in (
                (f"annoteren/{run}/frame-{step:04d}.png", shot.get("sha256")),
                (f"context/{run}/prev-for-{step:04d}.png", predecessor.get("sha256"))):
                data = file_bytes(root, path)
                demand(len(data) >= 24 and data.startswith(PNG_SIGNATURE) and
                       struct.unpack(">II", data[16:24]) == (390, 844) and
                       sha(data) == expected,
                       "Original screenshot byte integrity check failed.")
            allowed.add(f"annoteren/{run}/frame-{step:04d}.png")
            seen.add(shot["sha256"])
    demand(len(seen) == manifest.get("uniqueOriginalTargetDigests"),
           "Frozen duplicate screenshot accounting changed.")
    return allowed


def check_bind(host, lan):
    try:
        ip = ipaddress.ip_address(host)
    except ValueError as exc:
        raise ValueError("Provide a numeric IPv4 address, not a hostname.") from exc
    demand(ip.version == 4 and not ip.is_unspecified and not ip.is_multicast,
           "Only a specific IPv4 interface is allowed; never 0.0.0.0.")
    if lan:
        demand(ip.is_private and not ip.is_loopback and not ip.is_link_local,
               "LAN requires an explicitly chosen private Wi-Fi IPv4 address.")
    else:
        demand(ip.is_loopback, "Use --lan to bind the private Wi-Fi interface.")
    return str(ip)


def serve(root, host="127.0.0.1", port=8765, lan=False, started=None):
    host = check_bind(host, lan)
    allowed = source_index(root)
    token = secrets.token_urlsafe(32)

    class Handler(SimpleHTTPRequestHandler):
        def __init__(self, *args, **kwargs):
            super().__init__(*args, directory=str(root.resolve()), **kwargs)

        def log_message(self, fmt, *args):
            # Do not log paths, bearer token, reviewer state or device identity.
            pass

        def end_headers(self):
            self.send_header("Cache-Control", "no-store, max-age=0")
            self.send_header("Referrer-Policy", "no-referrer")
            self.send_header("X-Content-Type-Options", "nosniff")
            self.send_header("X-Frame-Options", "DENY")
            self.send_header("Content-Security-Policy",
                             "default-src 'none'; img-src 'self'; "
                             "script-src 'unsafe-inline'; style-src 'unsafe-inline'; "
                             "connect-src 'none'; object-src 'none'; "
                             "frame-ancestors 'none'; base-uri 'none'; form-action 'none'")
            super().end_headers()

        def authorized(self):
            try:
                cookie = SimpleCookie()
                cookie.load(self.headers.get("Cookie", ""))
                return ("argus_private" in cookie and
                        secrets.compare_digest(cookie["argus_private"].value, token))
            except Exception:
                return False

        def do_GET(self):
            request = urlsplit(self.path)
            if request.path == "/" and parse_qs(request.query).get("token") == [token]:
                self.send_response(HTTPStatus.SEE_OTHER)
                self.send_header("Location", "/")
                self.send_header("Set-Cookie",
                                 f"argus_private={token}; Path=/; SameSite=Strict; HttpOnly")
                self.end_headers()
                return
            if not self.authorized():
                self.send_error(HTTPStatus.FORBIDDEN, "Private review authorization required.")
                return
            path = request.path.lstrip("/")
            if request.path == "/":
                path = "index.html"
            elif re.fullmatch(r"/annoteren/m238-[abc]/", request.path):
                path = request.path.lstrip("/") + "index.html"
            if path not in allowed or request.query:
                self.send_error(HTTPStatus.NOT_FOUND, "No such private review file.")
                return
            self.path = "/" + path
            super().do_GET()

        def do_HEAD(self):
            self.send_error(HTTPStatus.METHOD_NOT_ALLOWED)

        def do_POST(self):
            self.send_error(HTTPStatus.METHOD_NOT_ALLOWED)

        def do_PUT(self):
            self.send_error(HTTPStatus.METHOD_NOT_ALLOWED)

    with ThreadingHTTPServer((host, port), Handler) as server:
        url = f"http://{host}:{server.server_port}/?token={token}"
        print("ARGUS private ORIGINAL review (read-only; no image/label uploads).", flush=True)
        print("Open this address on your computer or trusted Wi-Fi iPhone:", flush=True)
        print(url, flush=True)
        print("Close with Ctrl+C. Do NOT forward/share this link or use untrusted Wi-Fi.", flush=True)
        if started is not None:
            started(server, url)
        else:
            try:
                server.serve_forever(poll_interval=0.1)
            except KeyboardInterrupt:
                pass


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--review", type=Path, default=Path(__file__).parent)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--lan", action="store_true",
                        help="Explicit trusted Wi-Fi only: --lan --host 192.168.x.y")
    args = parser.parse_args()
    serve(args.review, args.host, args.port, args.lan)


if __name__ == "__main__":
    main()
