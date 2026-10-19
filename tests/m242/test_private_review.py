"""M2.42 synthetic-only authenticated LOCAL server and immutable kit tests."""
import http.client
import json
import sys
import tempfile
import threading
import unittest
import urllib.parse
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "m238"))
from test_fresh_capture import FreshCaptureSyntheticTests
from tools.m238 import fresh_capture
from tools.m242.package_private_review import pack, KIT_ROOT
from tools.m242.private_review_server import check_bind, serve, source_index


class PrivateReviewKitTests(unittest.TestCase):
    def setUp(self):
        self.fixture = FreshCaptureSyntheticTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.tearDown)
        self.review = self.fixture.root / "review"
        fresh_capture.freeze(self.fixture.captures, self.review)

    def test_validOriginalOnly_ProducesPrivate115MemberKitWithoutTraces(self):
        output = self.fixture.root / "private-review-kit.zip"
        result = pack(self.fixture.captures, self.review, output)
        self.assertEqual("PRIVATE_ORIGINAL_REVIEW_KIT_READY", result["status"])
        self.assertFalse(result["containsOwnerTruth"])
        with zipfile.ZipFile(output) as archive:
            self.assertIsNone(archive.testzip())
            members = archive.namelist()
            self.assertEqual(115, len(members))
            self.assertIn(f"{KIT_ROOT}/private_review_server.py", members)
            self.assertEqual(54, len([name for name in members if
                                      name.startswith(f"{KIT_ROOT}/annoteren/") and
                                      name.endswith(".png")]))
            self.assertFalse(any("trace.jsonl" in name or "labels.json" in name for name in members))
            self.assertEqual(
                (self.review / "study-manifest.json").read_bytes(),
                archive.read(f"{KIT_ROOT}/study-manifest.json"))
        with self.assertRaisesRegex(ValueError, "Refuse existing output"):
            pack(self.fixture.captures, self.review, output)

    def test_changedSourceOriginalOrFrozenCopy_RejectsPackaging(self):
        affected = next((self.review / "annoteren" / "m238-a").glob("frame-*.png"))
        affected.write_bytes(b"not-original")
        with self.assertRaises((ValueError, OSError)):
            pack(self.fixture.captures, self.review, self.fixture.root / "bad.zip")

    def test_localAndPrivateInterfaceValidation_RefusesWildcardOrPublic(self):
        self.assertEqual("127.0.0.1", check_bind("127.0.0.1", False))
        self.assertEqual("192.168.1.23", check_bind("192.168.1.23", True))
        for host, lan in (("0.0.0.0", True), ("8.8.8.8", True),
                          ("192.168.1.23", False), ("127.0.0.1", True),
                          ("example.org", True), ("169.254.2.10", True)):
            with self.subTest(host=host, lan=lan):
                with self.assertRaises(ValueError):
                    check_bind(host, lan)

    def test_maliciousHtmlNetworkReferences_RefuseToServeOriginal(self):
        html = self.review / "annoteren" / "m238-b" / "index.html"
        html.write_text(html.read_text() + '<script>fetch("http://x")</script>')
        with self.assertRaisesRegex(ValueError, "reviewer script"):
            source_index(self.review)

    def test_originalBytesAndExactly54AllowlistedScreenshots(self):
        allowed = source_index(self.review)
        self.assertEqual(58, len(allowed))
        self.assertIn("index.html", allowed)
        self.assertIn("annoteren/m238-c/index.html", allowed)
        self.assertNotIn("study-manifest.json", allowed)
        self.assertFalse(any("context/" in name for name in allowed))

    def test_httpTokenRequiredAndNoPrivateContextOrManifestFiles(self):
        ready = threading.Event()
        holder = {}
        def running(server, url):
            holder.update(server=server, url=url)
            ready.set()
            server.serve_forever(poll_interval=.05)
        thread = threading.Thread(target=serve,
                                  args=(self.review, "127.0.0.1", 0, False, running),
                                  daemon=True)
        thread.start()
        self.assertTrue(ready.wait(10))
        self.addCleanup(lambda: (holder["server"].shutdown(), thread.join(5)))
        url = urllib.parse.urlsplit(holder["url"])
        conn = http.client.HTTPConnection("127.0.0.1", url.port, timeout=10)
        self.addCleanup(conn.close)

        def get(path, headers=None, method="GET"):
            conn.request(method, path, headers=headers or {})
            response = conn.getresponse()
            return response.status, dict(response.getheaders()), response.read()

        status, headers, data = get("/")
        self.assertEqual(403, status)
        self.assertNotIn(b"frame-", data)
        status, headers, data = get(f"/?token={urllib.parse.parse_qs(url.query)['token'][0]}")
        self.assertEqual(303, status)
        self.assertEqual("/", headers["Location"])
        self.assertIn("HttpOnly", headers["Set-Cookie"])
        cookie = headers["Set-Cookie"].split(";", 1)[0]
        access = {"Cookie": cookie}

        self.assertEqual(200, get("/", access)[0])
        self.assertEqual(200, get("/annoteren/m238-a/", access)[0])
        manifest = json.loads((self.review / "study-manifest.json").read_text())
        frame = manifest["runs"][0]["images"][0]["file"]
        status, headers, png = get(f"/annoteren/m238-a/{frame}", access)
        self.assertEqual(200, status)
        self.assertEqual(fresh_capture.sha(png), manifest["runs"][0]["images"][0]["sha256"])
        self.assertEqual("no-store, max-age=0", headers["Cache-Control"])
        for forbidden in ("/study-manifest.json", "/context/m238-a/prev-for-0001.png",
                          "/README-FIRST.txt", "/../study-manifest.json"):
            self.assertEqual(404, get(forbidden, access)[0], forbidden)
        self.assertEqual(405, get("/", access, method="HEAD")[0])
        self.assertEqual(405, get("/", access, method="POST")[0])
        self.assertEqual(403, get("/annoteren/m238-a/", {"Cookie": "argus_private=wrong"})[0])


if __name__ == "__main__":
    unittest.main()
