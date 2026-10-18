"""Serve the generated review artifact and test a REAL mobile Chromium interaction."""
import argparse
import json
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from threading import Thread

from playwright.sync_api import sync_playwright


def verify(site: Path, runs: tuple[str, ...] = ("dev-a", "dev-b")) -> None:
    handler = partial(SimpleHTTPRequestHandler, directory=str(site.resolve()))
    with ThreadingHTTPServer(("127.0.0.1", 0), handler) as server:
        Thread(target=server.serve_forever, daemon=True).start()
        with sync_playwright() as browser_engine:
            browser = browser_engine.chromium.launch(channel="chrome", headless=True)
            try:
                for name in runs:
                    context = browser.new_context(viewport={"width": 390, "height": 844},
                                                  is_mobile=True, has_touch=True, accept_downloads=True)
                    page = context.new_page()
                    errors = []
                    page.on("pageerror", lambda error: errors.append(str(error)))
                    page.goto(f"http://127.0.0.1:{server.server_port}/annoteren/{name}/", wait_until="load")
                    img = page.locator("#shot")
                    img.wait_for()
                    assert img.evaluate("(i) => i.complete && i.naturalWidth === 390")
                    total = int(page.locator("#index").inner_text().split(" / ")[1])
                    assert 1 <= total <= 64
                    assert page.locator("#index").inner_text() == f"1 / {total}"
                    page.locator("#next").click()
                    assert total >= 2
                    assert page.locator("#index").inner_text() == f"2 / {total}"
                    page.locator("#previous").click()
                    img.click(position={"x": 140, "y": 230})
                    assert "gemarkeerd" in page.locator("#player-state").inner_text()
                    page.locator("#threat-mode").click()
                    img.click(position={"x": 75, "y": 160})
                    assert "1 gemarkeerd" in page.locator("#threat-state").inner_text()
                    with page.expect_download() as info:
                        page.locator("#download").click()
                    export = json.loads(Path(info.value.path()).read_text())
                    assert export["runId"] == name
                    assert len(export["frames"]) == total
                    assert export["frames"][0]["player"]["state"] == "visible"
                    assert export["frames"][1]["player"]["state"] == "unknown"
                    page.reload()
                    assert "gemarkeerd" in page.locator("#player-state").inner_text()
                    assert not errors, errors
                    context.close()
                print(f"PASS: {len(runs)} real mobile review sites, taps, navigation, JSON export and reload persistence.")
            finally:
                browser.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--site", type=Path, required=True)
    parser.add_argument("--runs", nargs="+", default=["dev-a", "dev-b"])
    options = parser.parse_args()
    verify(options.site, tuple(options.runs))
