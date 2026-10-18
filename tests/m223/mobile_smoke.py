"""M2.23 synthetic and genuine original-PNG blind mobile reviewer in Chromium."""
import argparse
import json
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from threading import Thread
from playwright.sync_api import expect, sync_playwright


def smoke(site: Path):
    with ThreadingHTTPServer(("127.0.0.1",0),
            partial(SimpleHTTPRequestHandler,directory=str(site.resolve()))) as server:
        Thread(target=server.serve_forever,daemon=True).start()
        with sync_playwright() as engine:
            browser=engine.chromium.launch(headless=True)
            try:
                for run in ("dev-l","dev-m","dev-n"):
                    context=browser.new_context(viewport={"width":375,"height":844},
                                                is_mobile=True,has_touch=True,accept_downloads=True)
                    page=context.new_page()
                    errors=[]
                    page.on("pageerror",lambda e:errors.append(str(e)))
                    page.goto(f"http://127.0.0.1:{server.server_port}/annoteren/{run}/",
                              wait_until="load")
                    image=page.locator("#shot")
                    expect(image).to_have_attribute("alt","Originele screenshot. Tik op de zichtbare speler.")
                    assert image.evaluate("(n)=>n.complete&&n.naturalWidth===390&&n.naturalHeight===844")
                    for width,point in ((320,(25,310)),(375,(350,415)),(430,(99,540))):
                        page.set_viewport_size({"width":width,"height":844})
                        bounds=image.bounding_box()
                        page.touchscreen.tap(bounds["x"]+point[0]*bounds["width"]/390,
                                             bounds["y"]+point[1]*bounds["height"]/844)
                        ring=page.locator("#ring")
                        got=ring.evaluate("(n)=>[+n.getAttribute('cx'),+n.getAttribute('cy')]")
                        assert abs(got[0]-point[0])<=1 and abs(got[1]-point[1])<=1,(run,got,point)
                        rect=ring.bounding_box()
                        assert rect and abs(rect["width"]-rect["height"])<.2
                    page.locator("#next").click()
                    page.locator("#absent").click()
                    page.locator("#next").click()
                    page.locator("#unknown").click()
                    with page.expect_download() as d:page.locator("#download").click()
                    data=json.loads(Path(d.value.path()).read_text())
                    assert d.value.suggested_filename==f"m223-{run}-player-points.json"
                    assert data["runId"]==run and len(data["frames"])==18
                    assert data["frames"][0]["player"]=={
                        "state":"visible","center":{"x":99,"y":540}}
                    assert data["frames"][1]["player"]=={"state":"absent","center":None}
                    assert all(f["player"]["state"]=="unknown" for f in data["frames"][2:])
                    page.reload()
                    expect(page.locator("#visible")).to_have_attribute("aria-pressed","true")
                    page.locator("#absent").click()
                    page.locator("#import").set_input_files({
                        "name":"local-owner.json","mimeType":"application/json",
                        "buffer":json.dumps(data).encode()})
                    expect(page.locator("#visible")).to_have_attribute("aria-pressed","true")
                    assert not errors,errors
                    context.close()
            finally:
                browser.close()
    print("PASS M2.23: three 18-frame blind original-PNG reviewers; 320/375/430 touch accuracy and private roundtrip.")


if __name__=="__main__":
    p=argparse.ArgumentParser()
    p.add_argument("--site",required=True,type=Path)
    smoke(p.parse_args().site)
