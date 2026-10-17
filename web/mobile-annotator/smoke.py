"""Actual browser interaction test of a *served HTTP* mobile annotator, not sandbox HTML preview."""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
from threading import Thread

from playwright.sync_api import sync_playwright


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--site", type=Path, required=True)
    args = parser.parse_args()

    handler = partial(SimpleHTTPRequestHandler, directory=str(args.site.resolve()))
    with ThreadingHTTPServer(("127.0.0.1", 0), handler) as http:
        Thread(target=http.serve_forever, daemon=True).start()
        url = f"http://127.0.0.1:{http.server_port}/annoteren/run-02/"
        with sync_playwright() as p:
            browser = p.chromium.launch(channel="chrome", headless=True)
            try:
                page = browser.new_page(viewport={"width": 390, "height": 844}, is_mobile=True,
                                        has_touch=True, accept_downloads=True)
                failures = []
                page.on("pageerror", lambda error: failures.append(str(error)))
                page.goto(url, wait_until="load")
                image = page.locator("#shot")
                image.wait_for()
                assert image.evaluate("(img) => img.complete && img.naturalWidth === 390")
                assert page.locator("#index").inner_text() == "1 / 12"

                page.locator("#next").click()
                assert page.locator("#index").inner_text() == "2 / 12"
                page.locator("#previous").click()
                assert page.locator("#index").inner_text() == "1 / 12"

                image.click(position={"x": 130, "y": 220})
                assert "gemarkeerd" in page.locator("#player-state").inner_text()
                page.locator("#threat-mode").click()
                image.click(position={"x": 65, "y": 130})
                assert "1 gemarkeerd" in page.locator("#threat-state").inner_text()

                with page.expect_download() as download_info:
                    page.locator("#download").click()
                download = download_info.value
                assert download.suggested_filename == "labels.json"
                payload = json.loads(Path(download.path()).read_text())
                assert payload["runId"] == "run-02" and len(payload["frames"]) == 12
                assert payload["frames"][0]["player"]["state"] == "visible"
                assert payload["frames"][0]["threats"]["state"] == "known"
                assert len(payload["frames"][0]["threats"]["centers"]) == 1
                assert payload["frames"][1]["player"]["state"] == "unknown"

                page.reload()
                assert "gemarkeerd" in page.locator("#player-state").inner_text()
                assert "1 gemarkeerd" in page.locator("#threat-state").inner_text()
                assert not failures, f"JavaScript errors: {failures}"
                print("PASS: actual hosted screenshot + mobile navigation + click labels + download + local reload.")
            finally:
                browser.close()


if __name__ == "__main__":
    main()
