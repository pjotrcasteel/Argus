# Future: Live Spectator Mode

## Goal
Watch one ARGUS-controlled game session live, showing the actual game feed, what the agent is observing and why it decided to move, click or wait. No game-side integration, hidden information, paid streaming platform or mandatory external cloud service.

## Proposed local-first architecture

1. The existing black-box game adapter captures screenshots and ordinary player actions.
2. A **bounded publish/subscribe frame stream** publishes downscaled JPEG previews (2–5 fps to start), timestamped decisions and confidence. The current PNG/JSONL evidence is the canonical durable trace.
3. A local ASP.NET Core host exposes a WebSocket or server-sent-events endpoint on loopback by default. Browser spectator clients never send gameplay commands.
4. A minimal static dashboard shows the live viewport, phase/reason, chosen action, estimated mage and possible-threat overlays, and counters.
5. One-click freeze/rewind uses saved frames and trace data locally, independent of real-time streaming.
6. Only if remote viewing is explicitly requested: authenticated, opt-in access with TLS. **Never** put a permanently public browser host, token or gameplay stream on GitHub Actions.

## Controls and costs

- Local only by default; no server bills or commercial inference APIs.
- Preview buffer bounded by a small configurable memory cap; dropping old spectator frames must never delay the agent.
- Viewer is read-only and must not change game state.
- When stopped, close the browser, dispose capture services, clear sensitive temporary frames and preserve only user-configured artifacts.
- Clearly distinguish estimated visual signals from true game state.

## Status
**Design only. Not implemented yet.** The next near-term step is robust movement perception and comparable multiple-run evidence; then local post-run replay before optional live viewing.
