"""True HTTP mobile browser test: original pixels, arbitrary coordinates, local-only JSON."""
import argparse
import json
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from threading import Thread
from playwright.sync_api import sync_playwright


def verify(site: Path):
    handler = partial(SimpleHTTPRequestHandler, directory=str(site.resolve()))
    with ThreadingHTTPServer(("127.0.0.1", 0), handler) as server:
        Thread(target=server.serve_forever, daemon=True).start()
        with sync_playwright() as engine:
            browser = engine.chromium.launch(headless=True)
            try:
                for run in ("dev-f", "dev-g", "dev-h"):
                    context = browser.new_context(viewport={"width":320,"height":844},
                        is_mobile=True, has_touch=True, accept_downloads=True)
                    page = context.new_page()
                    errors = []
                    page.on("pageerror", lambda e: errors.append(str(e)))
                    page.goto(f"http://127.0.0.1:{server.server_port}/annoteren/{run}-points/",
                              wait_until="load")
                    img = page.locator("#shot")
                    assert img.evaluate("(i)=>i.complete&&i.naturalWidth===390&&i.naturalHeight===844")
                    assert "0 beoordeeld" in page.locator("#status").inner_text()
                    assert page.locator("#previous").is_disabled()
                    assert page.locator("#point").get_attribute("visibility") == "hidden"
                    for width, target in ((320,(115,210)),(375,(310,650)),(430,(55,135))):
                        page.set_viewport_size({"width":width,"height":844})
                        r = img.bounding_box()
                        assert r
                        x = r["x"] + target[0] * r["width"] / 390
                        y = r["y"] + target[1] * r["height"] / 844
                        page.touchscreen.tap(x,y)
                        assert page.locator("#visible").get_attribute("aria-pressed") == "true"
                        actual = page.locator("#ring").evaluate("(n)=>[Number(n.getAttribute('cx')),Number(n.getAttribute('cy'))]")
                        assert abs(actual[0]-target[0])<=1 and abs(actual[1]-target[1])<=1, (width,target,actual)
                        bounds = page.locator("#ring").bounding_box()
                        assert bounds and abs(bounds["width"]-bounds["height"])<.2, (width,bounds)
                        expected = 30*r["width"]/390
                        assert abs(bounds["width"]-expected)<.3, (width,bounds,expected)
                    page.locator("#next").click()
                    page.locator("#absent").click()
                    assert "2 beoordeeld" in page.locator("#status").inner_text()
                    page.locator("#next").click()
                    page.locator("#unknown").click()
                    with page.expect_download() as event:
                        page.locator("#download").click()
                    file = event.value
                    assert file.suggested_filename == f"m218-{run}-player-points.json"
                    exported = json.loads(Path(file.path()).read_text())
                    assert exported["partition"] == "development"
                    assert exported["runId"] == run and len(exported["frames"]) == 12
                    assert exported["frames"][0]["player"]["center"] == {"x":55,"y":135}
                    assert exported["frames"][1]["player"] == {"state":"absent","center":None}
                    assert all(x["player"]["state"]=="unknown" for x in exported["frames"][2:])
                    page.reload()
                    assert "2 beoordeeld" in page.locator("#status").inner_text()
                    # One user's private local export can safely be restored.
                    page.locator("#absent").click()
                    page.locator("#import").set_input_files({
                        "name":"review.json","mimeType":"application/json",
                        "buffer":json.dumps(exported).encode()})
                    page.locator("#previous").click()
                    assert page.locator("#absent").get_attribute("aria-pressed") == "true"
                    page.locator("#previous").click()
                    assert page.locator("#visible").get_attribute("aria-pressed") == "true"
                    assert page.locator("#ring").get_attribute("cx") == "55"
                    assert not errors, errors
                    context.close()
            finally:
                browser.close()
    print("PASS: 3 original-image M2.18 mobile point pages, 3 viewports, arbitrary tap pixels, blind local JSON export/import/reload.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--site", type=Path, required=True)
    verify(parser.parse_args().site)
