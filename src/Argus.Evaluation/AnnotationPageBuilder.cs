using System.Text.Json;
using Argus.Replay;

namespace Argus.Evaluation;

/// <summary>
/// Generates a local, self-contained screenshot annotation UI. No game input, network, storage or external JS.
/// Labels are manually changed in the browser and downloaded as JSON for the independent offline evaluator.
/// </summary>
public static class AnnotationPageBuilder
{
    public static string Render(ReplayTrace replay, LabelSet template)
    {
        GroundTruth.Validate(template, template.RunId, replay);
        if (template.Frames.Count == 0) throw new InvalidDataException("Cannot annotate a trace with no selected frames.");

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var frames = JsonSerializer.Serialize(replay.Frames, jsonOptions);
        var labels = JsonSerializer.Serialize(template, jsonOptions);
        // Default .NET JSON escaping protects embedded script text, including <, >, &, and quotes.
        return Template.Replace("/*ARGUS_TRACE_FRAMES*/", frames, StringComparison.Ordinal)
            .Replace("/*ARGUS_ANNOTATIONS*/", labels, StringComparison.Ordinal);
    }

    private const string Template = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>ARGUS — Screen Annotation Studio</title>
<style>
:root{color-scheme:dark;font-family:system-ui,-apple-system,Segoe UI,sans-serif;background:#11141c;color:#e9eef8}
*{box-sizing:border-box}body{max-width:1250px;margin:auto;padding:clamp(12px,2vw,28px)}
h1{font-size:1.35rem;margin:.25rem 0}p{color:#aebbd0}main{display:grid;grid-template-columns:minmax(260px,1fr) minmax(275px,345px);gap:18px}
section{background:#1c2230;border:1px solid #39465b;border-radius:14px;padding:15px;min-width:0}
.viewport{position:relative;margin:0 auto;max-width:650px;line-height:0;background:#090b11}
.viewport img{width:100%;height:auto;display:block;user-select:none;cursor:crosshair}
.marker{position:absolute;transform:translate(-50%,-50%);width:19px;height:19px;border-radius:50%;border:2px solid;pointer-events:none;box-shadow:0 0 0 3px #07101aad}
.marker.player{border-color:#4df5ad}.marker.threat{border-color:#fba0a0}
.marker.predicted{border-color:#79b4ff;border-style:dashed;width:27px;height:27px;opacity:.85}
.marker.predicted-threat{border-color:#7bafff;border-style:dotted;width:22px;height:22px;opacity:.7}
#resume{max-width:220px;color:#c5d7f3}
.controls{display:flex;gap:8px;flex-wrap:wrap;margin-top:14px;align-items:center}
button{font:inherit;color:#eff3ff;border:1px solid #50607d;border-radius:9px;background:#2b3853;padding:9px 12px;cursor:pointer}
button:hover{background:#354768}button.selected{background:#235a60;border-color:#67d8c9}
button:focus-visible{outline:2px solid #9bcdff;outline-offset:3px}
button.primary{background:#296a4d;border-color:#50cc98}
button:disabled{opacity:.45;cursor:default}
fieldset{border:1px solid #41516d;border-radius:10px;margin:14px 0;padding:11px}
legend{padding:0 7px;color:#c0d0e5;font-weight:600}fieldset p{font-size:.82rem;margin:9px 0}
.row{display:flex;gap:8px;flex-wrap:wrap}.small{font-size:.82rem;color:#a7b6cc}
.status{margin-top:8px;color:#81e6ba}dl{display:grid;grid-template-columns:auto 1fr;gap:7px 12px;font-size:.86rem}
dt{color:#aebbd0}dd{margin:0;overflow-wrap:anywhere}
.notice{margin-top:16px;font-size:.83rem;line-height:1.5;color:#aebbd0}
@media(max-width:800px){main{grid-template-columns:1fr}}
</style>
</head>
<body>
<header><h1>ARGUS / Visual Ground Truth</h1><p>Local screenshots · manual annotations · no AI guessing · no network or game access</p></header>
<main>
<section>
  <div class="viewport" id="viewport"><img id="frame" alt="Recorded ARGUS gameplay frame; click to mark a visible object"></div>
  <div class="controls">
    <button id="previous" aria-label="Previous annotated frame">← Previous</button>
    <button id="next" aria-label="Next annotated frame">Next →</button>
    <span id="progress" class="small"></span>
  </div>
  <p class="small">Dotted blue circle = ARGUS player prediction. Green = your player label. Red = your threat labels. Click the image in the selected mode.</p>
</section>
<section>
  <div class="small" id="status"></div>
  <fieldset>
    <legend>1. Annotation mode</legend>
    <div class="row">
      <button id="mode-player" class="selected">Mark player</button>
      <button id="mode-threat">Mark threat</button>
    </div>
  </fieldset>
  <fieldset>
    <legend>2. Player</legend>
    <div class="row">
      <button id="player-absent">Player not visible</button>
      <button id="player-unknown">Can't determine</button>
    </div>
    <p id="player-state"></p>
  </fieldset>
  <fieldset>
    <legend>3. Dangerous objects</legend>
    <div class="row">
      <button id="threat-none">No visible threats</button>
      <button id="threat-unknown">Can't determine</button>
      <button id="threat-undo">Undo last point</button>
    </div>
    <p id="threat-state"></p>
  </fieldset>
  <dl>
    <dt>Frame</dt><dd id="frame-name"></dd>
    <dt>Agent action</dt><dd id="action"></dd>
    <dt>Reported reasoning</dt><dd id="reason"></dd>
  </dl>
  <div class="controls"><button id="export" class="primary">Export labels.json</button><label>Import previous labels.json <input id="resume" type="file" accept=".json,application/json"></label></div>
  <p class="notice">Export the JSON and place it in the same ARGUS run directory. Then use <code>Argus.Evaluation.Tool --labels</code> to calculate objective detection metrics. Unknown labels remain unknown; saving this file never trains an agent.</p>
</section>
</main>
<script>
"use strict";
const frames = /*ARGUS_TRACE_FRAMES*/;
const labels = /*ARGUS_ANNOTATIONS*/;
let dirty = false;
function changed() { dirty = true; }
function validatePoint(p, frame) {
  return p != null && Number.isFinite(p.x) && Number.isFinite(p.y) &&
    p.x >= 0 && p.y >= 0 && p.x < frame.width && p.y < frame.height;
}
function validImport(json) {
  if (!json || json.schemaVersion !== 1 || json.runId !== labels.runId ||
      !Array.isArray(json.frames) || json.frames.length !== labels.frames.length) return false;
  const byIndex = new Map(json.frames.map(f => [f.step, f]));
  if (byIndex.size !== labels.frames.length) return false;
  return labels.frames.every(item => {
    const entry = byIndex.get(item.step), frame = byStep.get(item.step);
    if (!entry || !frame || !entry.player || !entry.threats) return false;
    const playerOK = entry.player.state === "visible" ? validatePoint(entry.player.center, frame)
      : ["absent", "unknown"].includes(entry.player.state) && entry.player.center == null;
    const threatOK = entry.threats.state === "unknown" ? entry.threats.centers == null :
      entry.threats.state === "known" && Array.isArray(entry.threats.centers) &&
      entry.threats.centers.length <= 100 && entry.threats.centers.every(p => validatePoint(p, frame));
    return playerOK && threatOK;
  });
}
const byStep = new Map(frames.map(f => [f.step, f]));
const element = id => document.getElementById(id);
const viewport = element("viewport");
const image = element("frame");
let selected = 0;
let mode = "player";
const display = (value) => value == null ? "Unknown" : String(value);

function marker(point, kind) {
  const span = document.createElement("span");
  span.className = "marker " + kind;
  span.style.left = (point.x * 100 / byStep.get(labels.frames[selected].step).width) + "%";
  span.style.top = (point.y * 100 / byStep.get(labels.frames[selected].step).height) + "%";
  viewport.appendChild(span);
}

function render() {
  const label = labels.frames[selected];
  const frame = byStep.get(label.step);
  image.src = frame.screenshot;
  viewport.querySelectorAll(".marker").forEach(m => m.remove());
  if (frame.playerX != null && frame.playerY != null) marker({x: frame.playerX, y: frame.playerY}, "predicted");
  if (Array.isArray(frame.threatLocations))
    frame.threatLocations.forEach(p => marker(p, "predicted-threat"));
  if (label.player.state === "visible" && label.player.center) marker(label.player.center, "player");
  if (label.threats.state === "known") (label.threats.centers ?? []).forEach(p => marker(p, "threat"));
  element("frame-name").textContent = frame.screenshot;
  element("reason").textContent = frame.reason;
  element("action").textContent = frame.action ?? "No action";
  element("player-state").textContent = label.player.state === "visible" ? "Position marked"
    : label.player.state === "absent" ? "Explicitly not visible" : "Unknown / not reviewed";
  element("threat-state").textContent = label.threats.state === "known"
    ? (label.threats.centers?.length ?? 0) + " visible threat(s) annotated"
    : "Unknown / not reviewed";
  element("progress").textContent = (selected + 1) + " / " + labels.frames.length;
  const reviewed = labels.frames.filter(l => l.player.state !== "unknown" || l.threats.state !== "unknown").length;
  element("status").textContent = "Reviewed in at least one category: " + reviewed + "/" + labels.frames.length;
  element("mode-player").classList.toggle("selected", mode === "player");
  element("mode-threat").classList.toggle("selected", mode === "threat");
  element("previous").disabled = selected === 0;
  element("next").disabled = selected === labels.frames.length - 1;
}

function selectMode(next) { mode = next; render(); }
function updatePlayer(state, center) { labels.frames[selected].player = {state, center}; changed(); render(); }
function updateThreats(state, centers) { labels.frames[selected].threats = {state, centers}; changed(); render(); }
function go(delta) { selected = Math.max(0, Math.min(labels.frames.length - 1, selected + delta)); render(); }

image.addEventListener("click", event => {
  const rectangle = image.getBoundingClientRect();
  if (rectangle.width <= 0 || rectangle.height <= 0) return;
  const frame = byStep.get(labels.frames[selected].step);
  const x = Math.min(frame.width - 1, Math.max(0, Math.round((event.clientX - rectangle.left) * frame.width / rectangle.width)));
  const y = Math.min(frame.height - 1, Math.max(0, Math.round((event.clientY - rectangle.top) * frame.height / rectangle.height)));
  if (mode === "player") updatePlayer("visible", {x, y});
  else {
    const prior = labels.frames[selected].threats;
    const points = [...(prior.state === "known" ? prior.centers : []), {x, y}];
    if (points.length > 100) { element("status").textContent = "Maximum of 100 threat points per frame."; return; }
    if (points.slice(0,-1).some(p => Math.hypot(p.x-x,p.y-y) < 7)) return;
    updateThreats("known", points);
  }
});
element("mode-player").addEventListener("click", () => selectMode("player"));
element("mode-threat").addEventListener("click", () => selectMode("threat"));
element("player-absent").addEventListener("click", () => updatePlayer("absent", null));
element("player-unknown").addEventListener("click", () => updatePlayer("unknown", null));
element("threat-none").addEventListener("click", () => updateThreats("known", []));
element("threat-unknown").addEventListener("click", () => updateThreats("unknown", null));
element("threat-undo").addEventListener("click", () => {
  const prior = labels.frames[selected].threats;
  if (prior.state === "known" && prior.centers.length > 0)
    updateThreats("known", prior.centers.slice(0, -1));
});
element("previous").addEventListener("click", () => go(-1));
element("next").addEventListener("click", () => go(1));
document.addEventListener("keydown", event => {
  if (event.key === "ArrowLeft") go(-1);
  if (event.key === "ArrowRight") go(1);
});
element("resume").addEventListener("change", async event => {
  const file = event.target.files?.[0];
  if (!file || file.size > 2_000_000) {
    element("status").textContent = "Invalid or oversized annotation file.";
    event.target.value = "";
    return;
  }
  try {
    const imported = JSON.parse(await file.text());
    if (!validImport(imported)) throw new Error("Run, sample frames or pixel coordinates do not match.");
    const entries = new Map(imported.frames.map(entry => [entry.step, entry]));
    for (let i = 0; i < labels.frames.length; i++)
      labels.frames[i] = structuredClone(entries.get(labels.frames[i].step));
    dirty = false;
    render();
    element("status").textContent += " · Previous human labels restored locally.";
  } catch (error) {
    element("status").textContent = "Import rejected: " + error.message;
  } finally {
    event.target.value = "";
  }
});
element("export").addEventListener("click", () => {
  if (!validImport(labels)) { element("status").textContent = "Invalid label coordinates; export blocked."; return; }
  const blob = new Blob([JSON.stringify(labels, null, 2)], {type: "application/json"});
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = "labels.json";
  anchor.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
  dirty = false;
  element("status").textContent = "Download requested. Verify labels.json was saved before closing.";
});
window.addEventListener("beforeunload", event => {
  if (dirty) { event.preventDefault(); event.returnValue = ""; }
});
render();
</script>
</body>
</html>
""";
}
