using System.Text.Json;

namespace Argus.Replay;

/// <summary>Generates a portable offline HTML page using only local screenshots; no server or external dependencies.</summary>
public static class ReplayPageBuilder
{
    public static string Render(ReplayTrace trace)
    {
        if (trace.Frames.Count == 0) throw new InvalidDataException("Cannot render an empty replay.");

        // System.Text.Json's default encoder escapes <, >, &, quotes and other script-sensitive characters.
        // All dynamic text is later assigned to textContent, never innerHTML.
        var json = JsonSerializer.Serialize(trace.Frames, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Template.Replace("/*ARGUS_FRAMES*/", json, StringComparison.Ordinal);
    }

    private const string Template = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>ARGUS — Local Session Replay</title>
<style>
:root{color-scheme:dark;font-family:system-ui,-apple-system,Segoe UI,sans-serif;background:#101217;color:#edf0f8}
*{box-sizing:border-box}body{margin:0;padding:clamp(12px,2vw,30px);max-width:1300px;margin:auto}
h1{font-size:1.25rem;margin:0}p{color:#a8b1c4}header{display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap;margin-bottom:18px}
.label{font-size:.8rem;color:#a8b1c4;letter-spacing:.04em}main{display:grid;grid-template-columns:minmax(240px,1fr) minmax(260px,360px);gap:16px}
.panel{background:#1a1e28;border:1px solid #31394a;border-radius:14px;padding:14px;min-width:0}
.viewport{position:relative;max-width:600px;margin:auto;line-height:0;background:#08090d}
.viewport img{display:block;width:100%;height:auto}
#marker{position:absolute;width:19px;height:19px;border:2px solid #7ef2d0;border-radius:50%;transform:translate(-50%,-50%);box-shadow:0 0 0 4px #071a16aa;pointer-events:none;display:none}
.controls{display:flex;align-items:center;gap:8px;flex-wrap:wrap;margin-top:16px}
button{font:inherit;border:1px solid #465571;background:#27344b;color:#f0f3fa;border-radius:9px;padding:9px 13px;cursor:pointer}
button:hover{background:#354667}button:focus-visible,input:focus-visible{outline:2px solid #83baf4;outline-offset:3px}
input[type=range]{flex:1;min-width:130px;accent-color:#83baf4}
dt{color:#98a7bc;font-size:.8rem;margin-top:14px}dd{margin:4px 0 0;overflow-wrap:anywhere;line-height:1.45}
#reason{white-space:pre-wrap}.small{font-size:.8rem;color:#a8b1c4}.status{color:#7ef2d0}
@media(max-width:750px){main{grid-template-columns:1fr}}
</style>
</head>
<body>
<header><div><h1>ARGUS / Session Replay</h1><p class="small">Offline evidence · read-only · not a live stream or verified game state</p></div><span class="status">LOCAL REPLAY</span></header>
<main>
<section class="panel">
  <div class="viewport"><img id="frame" alt="Recorded game screenshot"><span id="marker" title="Inferred player position"></span></div>
  <div class="controls">
    <button id="back" aria-label="Previous frame">←</button>
    <button id="play" aria-label="Play or pause replay">Play</button>
    <button id="next" aria-label="Next frame">→</button>
    <input id="seek" type="range" min="0" value="0" aria-label="Seek to a frame">
  </div>
  <p class="small" id="progress"></p>
</section>
<aside class="panel">
  <div class="label">VISIBLE DECISION</div>
  <dl>
    <dt>Phase</dt><dd id="phase"></dd>
    <dt>Action</dt><dd id="action"></dd>
    <dt>Reason</dt><dd id="reason"></dd>
    <dt>Confidence (agent-reported)</dt><dd id="confidence"></dd>
    <dt>Possible visual threats</dt><dd id="threats"></dd>
    <dt>Observation time (UTC)</dt><dd id="time"></dd>
  </dl>
  <p class="small">The marker is an estimated pixel position; it can be wrong. No enemy identity, health, victory or fairness is inferred.</p>
</aside>
</main>
<script>
"use strict";
const frames = /*ARGUS_FRAMES*/;
const element = id => document.getElementById(id);
const seek = element("seek");
const preview = element("frame");
const marker = element("marker");
const play = element("play");
let index = 0;
let timer = null;
seek.max = String(frames.length - 1);
function render() {
  const f = frames[index];
  preview.src = f.screenshot;
  element("progress").textContent = "Frame " + (index + 1) + " of " + frames.length;
  element("phase").textContent = f.phase;
  element("action").textContent = f.action ?? "No further action";
  element("reason").textContent = f.reason;
  element("confidence").textContent = f.confidence == null ? "Unknown" : (f.confidence * 100).toFixed(1) + "%";
  element("threats").textContent = f.threatCandidates == null ? "Unknown" : String(f.threatCandidates) + " unverified candidate(s)";
  element("time").textContent = f.timestampUtc;
  marker.style.display = f.playerX == null || f.playerY == null ? "none" : "block";
  if (f.playerX != null && f.playerY != null) {
    marker.style.left = (f.playerX * 100 / f.width) + "%";
    marker.style.top = (f.playerY * 100 / f.height) + "%";
  }
  seek.value = String(index);
}
function stop() { if (timer !== null) { clearInterval(timer); timer = null; } play.textContent = "Play"; }
function jump(to) { index = Math.max(0, Math.min(frames.length - 1, to)); render(); }
element("back").addEventListener("click", () => { stop(); jump(index - 1); });
element("next").addEventListener("click", () => { stop(); jump(index + 1); });
seek.addEventListener("input", () => { stop(); jump(Number(seek.value)); });
play.addEventListener("click", () => {
  if (timer !== null) { stop(); return; }
  if (index === frames.length - 1) jump(0);
  play.textContent = "Pause";
  timer = setInterval(() => { if (index >= frames.length - 1) stop(); else jump(index + 1); }, 700);
});
document.addEventListener("keydown", e => {
  if (e.target === seek) return;
  if (e.key === "ArrowLeft") { stop(); jump(index - 1); }
  if (e.key === "ArrowRight") { stop(); jump(index + 1); }
});
render();
</script>
</body>
</html>
""";
}
