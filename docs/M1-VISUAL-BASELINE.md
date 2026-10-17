# M1 visual-agent vertical slice (October 2026)

ARGUS drives an **unmodified** browser game via Playwright screenshots, ordinary mouse clicks and keyboard presses. No DOM, hidden game state, source integration or telemetry hooks.

## What it does

1. Records viewport screenshots.
2. Runs **free local Tesseract OCR** over the screenshot and detects human-visible text bounding boxes.
3. Selects the highest-priority matching menu text from a separate game profile; clicks the OCR-detected screen location (never a fixed coordinate).
4. After a visible menu selection, it experimentally steers using changed pixels in four screen quadrants. This movement is a deliberately weak baseline, not combat intelligence.
5. Writes screenshot frames and JSONL action + evidence records for offline review.

## What it does NOT do

- It does not learn, infer game state, solve boss mechanics, optimize builds or certify UX.
- It cannot yet reliably distinguish route maps, inventory, menus and combat without better perception. Its experimental movement can be wrong.
- There has been no validated human-equivalent playthrough or actual WyrmForge browser run.
- Gameplay success and game completeness must be measured from visual video/screenshots, never inferred from a button click.

## Local run

Install .NET 10, Chromium for Playwright and free Tesseract OCR (Windows: Tesseract installer on PATH; Ubuntu: apt install tesseract-ocr).

Build once and install the bundled Chromium browser:

    dotnet restore Argus.slnx
    dotnet build Argus.slnx
    pwsh src/Argus.Adapters.Browser/bin/Debug/net10.0/playwright.ps1 install chromium

Start a bounded visual experiment:

    dotnet run --project src/Argus.Runner -- --url https://pjotrcasteel.github.io/Wyrmforge/ --agent visual --profile examples/wyrmforge.profile.json --output results/wyrmforge-visual --max-steps 60 --timeout-seconds 180

Default mode is observation only. Add --headless to suppress the browser window. Stop with Ctrl+C.

## Free-first constraints

No paid model or SaaS; screenshots/trace remain in ignored local results. CI only builds/tests pure logic; live browser testing and model training run locally. Future AI modules will consume the same external observation/action contract.

## Next practical gates

- Verify the screenshot-to-Tesseract OCR alignment in a real WyrmForge session.
- Add image-first control detection for labels which OCR misses.
- Distinguish static menu, route map and active combat without assuming gameplay.
- Add credible perception of player/enemy/hazard positions before calling movement intelligent.
- Test with held-out visual scenarios on portrait/mobile and desktop.
