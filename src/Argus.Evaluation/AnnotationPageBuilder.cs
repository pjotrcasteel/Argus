using System.Text.Json;
using Argus.Replay;

namespace Argus.Evaluation;

/// <summary>
/// Produces a local-only human annotation UI from validated trace frames.
/// Prediction markers are distinguished from manual ground truth; no labels are inferred.
/// </summary>
public static class AnnotationPageBuilder
{
    public static string Render(ReplayTrace trace, LabelSet template)
    {
        if (trace.Frames.Count == 0) throw new InvalidDataException("Cannot annotate an empty replay.");
        GroundTruth.Validate(template, template.RunId, trace);

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        // The standard encoder escapes HTML/script control characters before JSON is embedded in the page.
        var framesJson = JsonSerializer.Serialize(trace.Frames, options);
        var labelsJson = JsonSerializer.Serialize(template, options);
        return Template.Replace("/*ARGUS_FRAMES*/", framesJson, StringComparison.Ordinal)
            .Replace("/*ARGUS_LABELS*/", labelsJson, StringComparison.Ordinal);
    }

    private const string Template = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>ARGUS | Offline Perception Annotation</title>
<style>
:root{color-scheme:dark;font-family:system-ui,-apple-system,sans-serif;background:#10131a;color:#e8edf7}
*{box-sizing:border-box}body{margin:auto;max-width:1340px;padding:clamp(12px,2.5vw,28px)}
header{display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap;margin-bottom:16px}
h1{font-size:1.25rem;margin:0}.muted{color:#a7b4c8;font-size:.86rem}
main{display:grid;grid-template-columns:minmax(270px,1fr) minmax(250px,340px);gap:14px}
.panel{border:1px solid #354157;background:#1c2330;border-radius:14px;padding:14px;min-width:0}
.stage{position:relative;max-width:620px;margin:auto;background:#070a12;cursor:crosshair;touch-action:manipulation}
.stage img{display:block;width:100%;height:auto;user-select:none;-webkit-user-drag:none}
.stage svg{position:absolute;inset:0;width:100%;height:100%;pointer-events:none}
.controls{display:flex;gap:8px;flex-wrap:wrap;margin:12px 0}
button,.file-label{font:inherit;border:1px solid #56627b;background:#2a3851;color:#f2f5fc;padding:9px 12px;border-radius:8px;cursor:pointer}
button[aria-pressed=true]{background:#386180;border-color:#9bccff}
button:focus-visible,.file-label:focus-within{outline:2px solid #8ac9ff;outline-offset:2px}
button:hover,.file-label:hover{background:#385171}
.actions{display:grid;grid-template-columns:1fr 1fr;gap:9px}
.actions button{min-height:42px}#step{font-variant-numeric:tabular-nums}
#reason{white-space:pre-wrap;overflow-wrap:anywhere}.file-label input{display:none}
footer{margin-top:12px}.notice{color:#f9cf91}.legend{display:flex;gap:12px;flex-wrap:wrap;margin:12px 0}
.dot{display:inline-block;width:11px;height:11px;border:2px solid;border-radius:50%;margin-right:5px}
.pred{border-color:#86bcff}.truth{border-color:#71f0b5}.danger{border-color:#ffc67a}
button:disabled{opacity:.35;cursor:default}
@media(max-width:780px){main{grid-template-columns:1fr}}
</style>
</head>
<body>
<header>
<div><h1>ARGUS / Perception Ground Truth</h1><p class="muted">Offline · human labels only · never modifies the game or agent</p></div>
<div class="controls"><button id="export">Download labels.json</button><label class="file-label">Import labels.json <input type="file" id="import" accept=".json,application/json"></label></div>
</header>
<main>
<section class="panel">
<div class="stage" id="stage" role="button" tabindex="0" aria-label="Click to mark the selected object in this game screenshot">
<img id="frame" alt="Captured game screenshot"><svg id="markers" xmlns="http://www.w3.org/2000/svg"></svg>
</div>
<div class="legend">
<span><span class="dot pred"></span>Agent prediction</span>
<span><span class="dot truth"></span>Human player</span>
<span><span class="dot danger"></span>Human threat</span>
</div>
<div class="controls">
<button id="previous">← Previous</button>
<button id="next">Next →</button>
<span id="step"></span>
</div>
<p class="muted" id="reason"></p>
</section>
<aside class="panel">
<p class="muted">Choose what a click on the screenshot means:</p>
<div class="controls">
<button id="player-mode" aria-pressed="true">Mark player</button>
<button id="threat-mode" aria-pressed="false">Mark threat</button>
</div>
<h3>Player</h3>
<div class="actions">
<button id="player-absent">Player absent</button>
<button id="player-unknown">Can't tell</button>
</div>
<p class="muted" id="player-status"></p>
<h3>Threats</h3>
<div class="actions">
<button id="threat-none">No threats visible</button>
<button id="threat-unknown">Can't tell</button>
<button id="undo">Undo last point</button>
<button id="clear">Clear threats</button>
</div>
<p class="muted" id="threat-status"></p>
<p class="notice">Unreviewed frames remain UNKNOWN. Do not copy agent predictions as human evidence without checking the screenshot.</p>
<p class="muted">Export before closing. Files stay on your device; no upload or cloud account. Run the existing ARGUS evaluator on the downloaded labels.json.</p>
<p class="muted" id="status" role="status" aria-live="polite"></p>
</aside>
</main>
<footer class="muted">Click coordinates are computed from the natural screenshot dimensions. Use the included screenshots and trace to cross-check ambiguous cases.</footer>
<script>
"use strict";
const frames = /*ARGUS_FRAMES*/;
const initial = /*ARGUS_LABELS*/;
const byId = name => document.getElementById(name);
const selected = initial.frames.map(f => structuredClone(f));
const frameByStep = new Map(frames.map(f => [f.step, f]));
let position = 0;
let mode = "player";
let dirty = false;
const svgNs = "http://www.w3.org/2000/svg";

function current() { return selected[position]; }
function imageFrame() { return frameByStep.get(current().step); }
function setStatus(message) { byId("status").textContent = message; }
function markDirty() { dirty = true; setStatus("Unsaved edits — download labels.json before closing."); }
function switchMode(next) {
  mode = next;
  byId("player-mode").setAttribute("aria-pressed", String(next === "player"));
  byId("threat-mode").setAttribute("aria-pressed", String(next === "threat"));
}
function circle(x, y, color, radius, dashed) {
  const dot = document.createElementNS(svgNs, "circle");
  dot.setAttribute("cx", String(x));
  dot.setAttribute("cy", String(y));
  dot.setAttribute("r", String(radius));
  dot.setAttribute("stroke", color);
  dot.setAttribute("stroke-width", "3");
  dot.setAttribute("fill", "none");
  if (dashed) dot.setAttribute("stroke-dasharray", "5 3");
  byId("markers").appendChild(dot);
}
function render() {
  const f = imageFrame(), label = current();
  byId("frame").src = f.screenshot;
  const overlay = byId("markers");
  overlay.setAttribute("viewBox", "0 0 " + f.width + " " + f.height);
  overlay.replaceChildren();

  // Dashed blue marks are old agent guesses. Human labels use distinct solid green/orange.
  if (f.playerX != null && f.playerY != null) circle(f.playerX, f.playerY, "#86bcff", 14, true);
  if (Array.isArray(f.threatLocations))
    for (const p of f.threatLocations) circle(p.x, p.y, "#86bcff", 10, true);
  if (label.player.state === "visible" && label.player.center)
    circle(label.player.center.x, label.player.center.y, "#71f0b5", 17, false);
  if (label.threats.state === "known" && Array.isArray(label.threats.centers))
    for (const p of label.threats.centers) circle(p.x, p.y, "#ffc67a", 13, false);

  byId("step").textContent = "Sample " + (position + 1) + "/" + selected.length + " · frame " + f.step;
  byId("reason").textContent = f.phase + " — " + f.reason;
  byId("player-status").textContent = "Human label: " + label.player.state;
  byId("threat-status").textContent = "Human label: " + label.threats.state +
    (label.threats.centers == null ? "" : " (" + label.threats.centers.length + " point(s))");
  byId("previous").disabled = position === 0;
  byId("next").disabled = position === selected.length - 1;
}
function clickToPoint(clientX, clientY) {
  const rect = byId("frame").getBoundingClientRect(), f = imageFrame();
  if (!rect.width || !rect.height) return null;
  const x = (clientX - rect.left) * f.width / rect.width;
  const y = (clientY - rect.top) * f.height / rect.height;
  if (x < 0 || y < 0 || x >= f.width || y >= f.height) return null;
  return {x: Math.round(Math.min(f.width - 0.01, x) * 100) / 100,
    y: Math.round(Math.min(f.height - 0.01, y) * 100) / 100};
}
function addPoint(p) {
  if (!p) return;
  const label = current();
  if (mode === "player") label.player = {state:"visible", center:p};
  else {
    if (label.threats.state !== "known") label.threats = {state:"known", centers:[]};
    if (label.threats.centers.length >= 100) { setStatus("Maximum 100 points per frame."); return; }
    if (label.threats.centers.some(q => Math.hypot(q.x - p.x, q.y - p.y) < 7)) return;
    label.threats.centers.push(p);
  }
  markDirty();
  render();
}
byId("stage").addEventListener("click", event => addPoint(clickToPoint(event.clientX,event.clientY)));
byId("stage").addEventListener("keydown", event => {
  if (event.key !== "Enter" && event.key !== " ") return;
  event.preventDefault();
  const box = byId("frame").getBoundingClientRect();
  addPoint(clickToPoint(box.left + box.width / 2, box.top + box.height / 2));
});
byId("player-mode").addEventListener("click", () => switchMode("player"));
byId("threat-mode").addEventListener("click", () => switchMode("threat"));
byId("player-absent").addEventListener("click", () => { current().player = {state:"absent",center:null}; markDirty(); render(); });
byId("player-unknown").addEventListener("click", () => { current().player = {state:"unknown",center:null}; markDirty(); render(); });
byId("threat-none").addEventListener("click", () => { current().threats = {state:"known",centers:[]}; markDirty(); render(); });
byId("threat-unknown").addEventListener("click", () => { current().threats = {state:"unknown",centers:null}; markDirty(); render(); });
byId("clear").addEventListener("click", () => { current().threats = {state:"known",centers:[]}; markDirty(); render(); });
byId("undo").addEventListener("click", () => {
  if (current().threats.state !== "known" || !current().threats.centers.length) return;
  current().threats.centers.pop(); markDirty(); render();
});
byId("previous").addEventListener("click", () => { position = Math.max(0,position-1); render(); });
byId("next").addEventListener("click", () => { position = Math.min(selected.length-1,position+1); render(); });
function isPoint(p, f) {
  return p != null && Number.isFinite(p.x) && Number.isFinite(p.y) &&
    p.x >= 0 && p.y >= 0 && p.x < f.width && p.y < f.height;
}
function validImport(obj) {
  if (!obj || obj.schemaVersion !== 1 || obj.runId !== initial.runId ||
    !Array.isArray(obj.frames) || obj.frames.length !== selected.length) return false;
  const byStep = new Map(obj.frames.map(f => [f.step,f]));
  if (byStep.size !== selected.length) return false;
  return selected.every(original => {
    const item = byStep.get(original.step), f = frameByStep.get(original.step);
    if (!item || !item.player || !item.threats) return false;
    const playerOK = item.player.state === "visible" ? isPoint(item.player.center,f)
      : ["unknown","absent"].includes(item.player.state) && item.player.center == null;
    const threatOK = item.threats.state === "unknown" ? item.threats.centers == null
      : item.threats.state === "known" && Array.isArray(item.threats.centers) &&
        item.threats.centers.length <= 100 && item.threats.centers.every(p => isPoint(p,f));
    return playerOK && threatOK;
  });
}
byId("import").addEventListener("change", async event => {
  const file = event.target.files?.[0];
  if (!file || file.size > 2_000_000) { setStatus("Invalid or oversized annotation file."); return; }
  try {
    const json = JSON.parse(await file.text());
    if (!validImport(json)) throw new Error("Annotation schema, sample steps or coordinates do not match this run.");
    const byStep = new Map(json.frames.map(f => [f.step,f]));
    for (let i = 0; i < selected.length; i++) selected[i] = structuredClone(byStep.get(selected[i].step));
    dirty = false; render(); setStatus("Annotations imported locally. Export after any further changes.");
  } catch (error) { setStatus("Import rejected: " + error.message); }
  event.target.value = "";
});
byId("export").addEventListener("click", () => {
  const payload = {schemaVersion:1, runId:initial.runId, frames:selected};
  if (!validImport(payload)) { setStatus("Invalid annotation state; export blocked."); return; }
  const blob = new Blob([JSON.stringify(payload,null,2)],{type:"application/json"});
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url; link.download = "labels.json";
  document.body.appendChild(link); link.click(); link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
  dirty = false;
  setStatus("Download requested. Confirm labels.json is saved before closing.");
});
window.addEventListener("beforeunload", event => { if (dirty) { event.preventDefault(); event.returnValue = ""; } });
render();
</script>
</body>
</html>
""";
}
