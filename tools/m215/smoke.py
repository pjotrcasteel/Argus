"""Real Chrome mobile touch/export/import QA on prediction-free M2.15 static site."""
import argparse
import json
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from threading import Thread

from playwright.sync_api import expect, sync_playwright


def smoke(site: Path):
    handler = partial(SimpleHTTPRequestHandler, directory=str(site.resolve()))
    with ThreadingHTTPServer(("127.0.0.1", 0), handler) as server:
        Thread(target=server.serve_forever, daemon=True).start()
        with sync_playwright() as browser_engine:
            browser = browser_engine.chromium.launch(headless=True)
            try:
                for run in ("dev-f", "dev-g", "dev-h"):
                    context = browser.new_context(viewport={"width": 390, "height": 844},
                                                  is_mobile=True, has_touch=True,
                                                  accept_downloads=True)
                    page = context.new_page()
                    errors = []
                    page.on("pageerror", lambda e: errors.append(str(e)))
                    page.goto(f"http://127.0.0.1:{server.server_port}/annoteren/{run}/",
                              wait_until="load")
                    shot = page.locator("#shot")
                    shot.wait_for()
                    assert shot.evaluate("(i) => i.complete && i.naturalWidth === 390 && i.naturalHeight === 844")
                    assert "locatie 1 / 24" in page.locator("#status").inner_text()
                    assert "0 handmatig beoordeeld" in page.locator("#status").inner_text()
                    left = page.locator("#ring").evaluate("(node) => node.style.left")
                    assert left.endswith("%")
                    assert page.locator("#previous").is_disabled()
                    # Human taps apply only to fixed locations, no screenshot position changes.
                    page.locator("#present").click()
                    assert page.locator("#present").get_attribute("aria-pressed") == "true"
                    assert "1 handmatig beoordeeld" in page.locator("#status").inner_text()
                    page.locator("#next").click()
                    assert "locatie 2 / 24" in page.locator("#status").inner_text()
                    page.locator("#absent").click()
                    assert "2 handmatig beoordeeld" in page.locator("#status").inner_text()
                    with page.expect_download() as dl:
                        page.locator("#download").click()
                    output = Path(dl.value.path())
                    exported = json.loads(output.read_text())
                    assert exported["runId"] == run and len(exported["probes"]) == 24
                    assert exported["probes"][0]["label"] == "present"
                    assert exported["probes"][1]["label"] == "absent"
                    assert all(p["label"] == "unknown" for p in exported["probes"][2:])
                    # Local-only persistence and checked import, no remote upload.
                    page.reload()
                    assert "2 handmatig beoordeeld" in page.locator("#status").inner_text()
                    page.locator("#unknown").click()
                    assert "1 handmatig beoordeeld" in page.locator("#status").inner_text()
                    page.locator("#import").set_input_files({
                        "name": "review.json", "mimeType": "application/json",
                        "buffer": json.dumps(exported).encode()})
                    expect(page.locator("#status")).to_contain_text("2 handmatig beoordeeld", timeout=5000)
                    expect(page.locator("#present")).to_have_attribute("aria-pressed", "true")

                    page.locator("#next").click()
                    assert page.locator("#absent").get_attribute("aria-pressed") == "true"
                    assert not errors, errors
                    context.close()
            finally:
                browser.close()
    print("PASS: 3 real mobile Chromium spatial review pages, PNG/circle/touch/export/import/reload; labels private.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--site", type=Path, required=True)
    options = parser.parse_args()
    smoke(options.site)
