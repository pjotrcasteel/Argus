"""Real Chromium mobile validation on invented OR frozen real original PNGs."""
import argparse
import json
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from threading import Thread
from playwright.sync_api import expect, sync_playwright

from tools.m238.fresh_capture import RUNS, TARGETS


def smoke(site):
    with ThreadingHTTPServer(("127.0.0.1",0),
            partial(SimpleHTTPRequestHandler, directory=str(site.resolve()))) as server:
        Thread(target=server.serve_forever, daemon=True).start()
        with sync_playwright() as playwright:
            browser = playwright.chromium.launch(headless=True)
            try:
                for run in RUNS:
                    context = browser.new_context(viewport={"width":375,"height":844},
                        is_mobile=True, has_touch=True, accept_downloads=True)
                    page = context.new_page()
                    errors = []
                    page.on("pageerror", lambda error: errors.append(str(error)))
                    page.goto(f"http://127.0.0.1:{server.server_port}/annoteren/{run}/")
                    image = page.locator("#shot")
                    expect(image).to_have_attribute(
                        "alt", "Originele screenshot. Tik op de zichtbare speler.")
                    assert image.evaluate("(n)=>n.complete&&n.naturalWidth===390&&n.naturalHeight===844")
                    for width,(x,y) in ((320,(25,310)),(375,(350,415)),(430,(99,540))):
                        page.set_viewport_size({"width":width,"height":844})
                        bounds = image.bounding_box()
                        page.touchscreen.tap(bounds["x"]+x*bounds["width"]/390,
                                             bounds["y"]+y*bounds["height"]/844)
                        circle = page.locator("#ring")
                        got = circle.evaluate("(n)=>[+n.getAttribute('cx'),+n.getAttribute('cy')]")
                        assert abs(got[0]-x)<=1 and abs(got[1]-y)<=1, (run,got,x,y)
                        ring_bounds=circle.bounding_box()
                        assert abs(ring_bounds["width"]-ring_bounds["height"])<.2
                    page.locator("#next").click()
                    page.locator("#absent").click()
                    page.locator("#next").click()
                    page.locator("#unknown").click()
                    with page.expect_download() as item:
                        page.locator("#download").click()
                    result=json.loads(Path(item.value.path()).read_text())
                    assert item.value.suggested_filename==f"{run}-player-points.json"
                    assert result["runId"]==run and len(result["frames"])==TARGETS
                    assert result["frames"][0]["player"]=={
                        "state":"visible","center":{"x":99,"y":540}}
                    assert result["frames"][1]["player"]=={"state":"absent","center":None}
                    assert all(x["player"]["state"]=="unknown" for x in result["frames"][2:])
                    page.reload()
                    expect(page.locator("#visible")).to_have_attribute("aria-pressed","true")
                    page.locator("#absent").click()
                    page.locator("#import").set_input_files({
                        "name":"owner-local.json","mimeType":"application/json",
                        "buffer":json.dumps(result).encode()})
                    expect(page.locator("#visible")).to_have_attribute("aria-pressed","true")
                    assert not errors, errors
                    context.close()
            finally:
                browser.close()
    print("PASS M2.38 private mobile originals 320/375/430 taps, circle geometry, export and import")


if __name__ == "__main__":
    p=argparse.ArgumentParser()
    p.add_argument("--site",type=Path,required=True)
    smoke(p.parse_args().site)
