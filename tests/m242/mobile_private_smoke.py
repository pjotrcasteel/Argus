"""M2.42 actual Chromium touch/export test against read-only LOCAL token server.

Uses synthetic PNGs on CI, never original WyrmForge sources or private labels.
"""
import argparse
import sys
import threading
from pathlib import Path

from playwright.sync_api import expect, sync_playwright

from tools.m242.private_review_server import RUNS, serve


def mobile_smoke(root):
    ready = threading.Event()
    obj = {}
    def on_start(server, url):
        obj.update(server=server, url=url)
        ready.set()
        server.serve_forever(poll_interval=.05)
    thread = threading.Thread(
        target=serve, args=(root, "127.0.0.1", 0, False, on_start), daemon=True)
    thread.start()
    assert ready.wait(10), "Private review server did not start."
    try:
        with sync_playwright() as driver:
            browser = driver.chromium.launch(headless=True)
            try:
                for run in RUNS:
                    context = browser.new_context(viewport={"width": 375, "height": 844},
                                                  has_touch=True, is_mobile=True,
                                                  accept_downloads=True)
                    page = context.new_page()
                    errors = []
                    page.on("pageerror", lambda e: errors.append(str(e)))
                    response = page.goto(obj["url"])
                    assert response.status == 200
                    assert "token=" not in page.url, "Bearer token must not survive clean redirect"
                    base = obj["url"].split("/?token=")[0]
                    page.goto(f"{base}/annoteren/{run}/")
                    shot = page.locator("#shot")
                    expect(shot).to_have_attribute(
                        "alt", "Originele screenshot. Tik op de zichtbare speler.")
                    assert shot.evaluate("(x)=>x.complete && x.naturalWidth===390 && x.naturalHeight===844")
                    for width, point in ((320, (75, 310)), (375, (300, 500)), (430, (121, 600))):
                        page.set_viewport_size({"width": width, "height": 844})
                        rectangle = shot.bounding_box()
                        page.touchscreen.tap(rectangle["x"] + point[0] * rectangle["width"] / 390,
                                             rectangle["y"] + point[1] * rectangle["height"] / 844)
                        circle = page.locator("#ring")
                        pixel = circle.evaluate("(x)=>[Number(x.getAttribute('cx')),Number(x.getAttribute('cy'))]")
                        assert abs(pixel[0] - point[0]) <= 1 and abs(pixel[1] - point[1]) <= 1, (run, pixel, point)
                    page.locator("#next").click()
                    page.locator("#absent").click()
                    page.locator("#next").click()
                    page.locator("#unknown").click()
                    with page.expect_download() as handle:
                        page.locator("#download").click()
                    download = handle.value
                    assert download.suggested_filename == f"{run}-player-points.json"
                    import json
                    exported = json.loads(Path(download.path()).read_text())
                    assert exported["runId"] == run and len(exported["frames"]) == 18
                    assert exported["frames"][0]["player"] == {
                        "state": "visible", "center": {"x": 121, "y": 600}}
                    assert exported["frames"][1]["player"] == {
                        "state": "absent", "center": None}
                    assert exported["frames"][2]["player"] == {
                        "state": "unknown", "center": None}
                    page.reload()
                    expect(page.locator("#visible")).to_have_attribute("aria-pressed", "true")
                    page.locator("#absent").click()
                    page.locator("#import").set_input_files({
                        "name": "owner-device.json", "mimeType": "application/json",
                        "buffer": json.dumps(exported).encode()})
                    expect(page.locator("#visible")).to_have_attribute("aria-pressed", "true")
                    assert not errors, (run, errors)
                    context.close()
            finally:
                browser.close()
    finally:
        obj["server"].shutdown()
        thread.join(5)
    print("PASS M2.42 token-protected original reviewer: mobile 320/375/430 taps, private export/import.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--review", required=True, type=Path)
    options = parser.parse_args()
    mobile_smoke(options.review)
