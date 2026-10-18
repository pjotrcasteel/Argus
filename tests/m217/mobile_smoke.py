"""True mobile Chromium acceptance: original private export is unchanged until a human edits."""
import importlib.util
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from threading import Thread
from playwright.sync_api import sync_playwright

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("m217_tests", HERE / "test_reference_audit.py")
tests = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tests)


def run():
    fixture = tests.Fixture()
    try:
        suspect = fixture.pairs[("dev-f", 0)][1]
        fixture.set("dev-f", suspect, "label", "present")
        source = fixture.labels["dev-f"].read_bytes()
        report, owner, images = tests.audit.inspect(fixture.zip, list(fixture.labels.values()), "")
        site = fixture.folder / "private-recheck.html"
        site.write_text(tests.audit.build_reviewer(report, owner, images), encoding="utf-8")
        handler = partial(SimpleHTTPRequestHandler, directory=str(fixture.folder))
        with ThreadingHTTPServer(("127.0.0.1", 0), handler) as server:
            Thread(target=server.serve_forever, daemon=True).start()
            with sync_playwright() as p:
                browser = p.chromium.launch(headless=True)
                try:
                    page = browser.new_page(viewport={"width": 320, "height": 740},
                                            is_mobile=True, has_touch=True, accept_downloads=True)
                    errors = []
                    page.on("pageerror", lambda e: errors.append(str(e)))
                    page.goto(f"http://127.0.0.1:{server.server_port}/private-recheck.html",
                              wait_until="load")
                    button = page.locator("#export")
                    assert button.is_disabled(), "Unresolved double-present must not export."
                    assert page.locator("select[data-sample]").count() == 2
                    for width in (320, 390, 430):
                        page.set_viewport_size({"width": width, "height": 740})
                        positions = page.evaluate("""() => [...document.querySelectorAll('.card')].map(c=>{
                          const svg=c.querySelector('svg');
                          const img=c.querySelector('img');
                          const circle=c.querySelector('circle');
                          const rect=circle.getBoundingClientRect();
                          const pic=img.getBoundingClientRect();
                          const scale=pic.width/390;
                          return {circleWidth:rect.width,circleHeight:rect.height, expected:52*scale,
                            expectedX:pic.left+Number(circle.getAttribute('cx'))*scale,
                            expectedY:pic.top+Number(circle.getAttribute('cy'))*scale,
                            centerX:rect.left+rect.width/2, centerY:rect.top+rect.height/2,
                            preserve:svg.getAttribute('preserveAspectRatio')};
                        })""")
                        assert len(positions) == 2
                        for v in positions:
                            assert v["preserve"] == "xMidYMid meet", v
                            assert abs(v["circleWidth"]-v["circleHeight"]) < .2, v
                            assert abs(v["circleWidth"]-v["expected"]) < .3, v
                            assert abs(v["centerX"]-v["expectedX"]) < .4, v
                            assert abs(v["centerY"]-v["expectedY"]) < .4, v
                    page.locator(f'select[data-sample="{suspect}"]').select_option("unknown")
                    assert button.is_enabled(), "Unknown requires explicit acknowledgment."
                    with page.expect_download() as download:
                        button.click()
                    downloaded = download.value
                    assert downloaded.suggested_filename == "m215-dev-f-labels-rechecked.json"
                    import json
                    corrected = json.loads(Path(downloaded.path()).read_text())
                    chosen = next(x for x in corrected["probes"] if x["sampleId"] == suspect)
                    assert chosen["label"] == "unknown"
                    assert len(corrected["probes"]) == 24
                    assert corrected["runId"] == "dev-f"
                    assert fixture.labels["dev-f"].read_bytes() == source, "Original owner JSON changed."
                    assert not errors, errors
                finally:
                    browser.close()
    finally:
        fixture.close()
    print("PASS: private conflict cannot export until explicit human choice; 320/390/430 SVG geometry, untouched originals.")


if __name__ == "__main__":
    run()
