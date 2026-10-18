"""Real Chromium mobile quality gate using *only* the synthetic/review images."""
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
                for run in ("dev-i","dev-j","dev-k"):
                    context=browser.new_context(viewport={"width":375,"height":844},
                                                is_mobile=True,has_touch=True,accept_downloads=True)
                    page=context.new_page()
                    errors=[]
                    page.on("pageerror",lambda e:errors.append(str(e)))
                    page.goto(f"http://127.0.0.1:{server.server_port}/annoteren/{run}/",
                              wait_until="load")
                    img=page.locator("#shot")
                    expect(img).to_have_attribute("alt","Originele screenshot. Tik op de zichtbare speler.")
                    assert img.evaluate("(n)=>n.complete&&n.naturalWidth===390&&n.naturalHeight===844")
                    assert "0 beoordeeld" in page.locator("#status").inner_text()
                    for width,point in ((320,(94,250)),(375,(333,405)),(430,(45,440))):
                        page.set_viewport_size({"width":width,"height":844})
                        bound=img.bounding_box()
                        page.touchscreen.tap(bound["x"]+point[0]*bound["width"]/390,
                                              bound["y"]+point[1]*bound["height"]/844)
                        expect(page.locator("#visible")).to_have_attribute("aria-pressed","true")
                        result=page.locator("#ring").evaluate("(n)=>[+n.getAttribute('cx'),+n.getAttribute('cy')]")
                        assert abs(result[0]-point[0])<=1 and abs(result[1]-point[1])<=1,(run,width,result)
                        rect=page.locator("#ring").bounding_box()
                        assert rect and abs(rect["width"]-rect["height"])<0.2
                        assert abs(rect["width"]-33*bound["width"]/390)<.4
                    page.locator("#next").click()
                    page.locator("#absent").click()
                    page.locator("#next").click()
                    page.locator("#unknown").click()
                    with page.expect_download() as dl:page.locator("#download").click()
                    result=dl.value
                    assert result.suggested_filename==f"m220-{run}-player-points.json"
                    data=json.loads(Path(result.path()).read_text())
                    assert data["runId"]==run and len(data["frames"])==18
                    assert data["frames"][0]["player"]=={"state":"visible","center":{"x":45,"y":440}}
                    assert data["frames"][1]["player"]=={"state":"absent","center":None}
                    assert all(v["player"]["state"]=="unknown" for v in data["frames"][2:])
                    page.reload()
                    expect(page.locator("#visible")).to_have_attribute("aria-pressed","true")
                    page.locator("#absent").click()
                    page.locator("#import").set_input_files({"name":"private.json","mimeType":"application/json",
                                                            "buffer":json.dumps(data).encode()})
                    expect(page.locator("#visible")).to_have_attribute("aria-pressed","true",timeout=5000)
                    assert not errors,errors
                    context.close()
            finally:
                browser.close()
    print("PASS: 3 real Chrome mobile pages, 18 frames each, arbitrary original-pixel tap, circular ring, unknown/absent + local private JSON.")

if __name__=="__main__":
    p=argparse.ArgumentParser()
    p.add_argument("--site",type=Path,required=True)
    smoke(p.parse_args().site)
