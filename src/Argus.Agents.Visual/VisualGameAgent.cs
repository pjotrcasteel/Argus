using Argus.Core;

namespace Argus.Agents.Visual;

/// <summary>
/// First black-box baseline: OCR-driven menus, then experimental motion-biased exploration.
/// This is a heuristic, NOT a trained policy or evidence of competent game completion.
/// </summary>
public sealed class VisualGameAgent(IFrameTextReader textReader, VisualGameProfile profile) : IVisualAgent, IAgentEvidenceProvider
{
    private byte[]? _previousFrame;
    private VisualBlob? _trackedPlayer;
    private byte[]? _previousCombatFrame;
    private CombatVisualObservation? _previousCombatObservation;
    private int _missedPlayerFrames;
    private int _menuClicks;
    private int _emptyScans;
    private int _explorationSteps;
    private bool _routeNodeSelected;
    private int _routeConfirmationAttempts;
    private bool _settling;
    private bool _entryProbeTried;

    public AgentEvidence? LastEvidence { get; private set; }

    public async ValueTask<GameAction?> DecideAsync(FrameObservation observation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_settling)
        {
            _previousCombatFrame = null;
            _previousCombatObservation = null;
            _settling = false;
            LastEvidence = new AgentEvidence("transition", "Allowing visual transition to finish", 1, []);
            _previousFrame = observation.PngBytes;
            return new GameAction(GameActionKind.Wait, DurationMs: 650);
        }

        // Fully inspect every entry/route frame. During established exploration,
        // OCR every third frame: screenshot motion and pixel perception still run
        // on every frame, while short-lived UI overlays are checked within two
        // subsequent observations. This prevents busy combat scenes from spending
        // most of the bounded experiment waiting for external OCR processes.
        var scanText = _explorationSteps == 0 || _explorationSteps % 3 == 0;
        var labels = scanText
            ? await textReader.ReadAsync(observation, cancellationToken)
            : Array.Empty<VisibleText>();
        var names = labels.Select(t => t.Text).Take(24).ToArray();
        var terminal = labels.Any(t => t.Confidence >= profile.MinimumTextConfidence &&
            profile.TerminalScreenCues.Any(cue => t.Text.Contains(cue, StringComparison.OrdinalIgnoreCase)));
        if (terminal)
        {
            LastEvidence = new AgentEvidence("terminal", "Detected visible result or progression screen; stopping input", 0.85f, names);
            return null;
        }
        var isChoiceScreen = labels.Any(t => t.Confidence >= profile.MinimumTextConfidence &&
            profile.ChoiceScreenCues.Any(cue => t.Text.Contains(cue, StringComparison.OrdinalIgnoreCase)));
        if (isChoiceScreen)
        {
            var selected = ChoiceCardMatcher.FindFirst(labels, profile.ChoiceCardTypes, profile.MinimumTextConfidence)
                ?? ChoiceCardMatcher.FindFirstTitleOnly(labels, profile.MinimumTextConfidence,
                    observation.Width, observation.Height);
            if (selected is null)
            {
                LastEvidence = new AgentEvidence("choice", "Visible choice screen, but no confidently located card title; waiting", 0, names);
                return new GameAction(GameActionKind.Wait, DurationMs: 600);
            }

            _settling = true;
            _previousFrame = observation.PngBytes;
            LastEvidence = new AgentEvidence("choice", "Choosing first visible upgrade card title: " + selected.Text,
                selected.Confidence / 100f, names);
            return new GameAction(GameActionKind.Click, X: selected.CenterX, Y: selected.CenterY);
        }

        var isRouteScreen = labels.Any(t => t.Confidence >= profile.MinimumTextConfidence &&
            profile.RouteCues.Any(cue => t.Text.Contains(cue, StringComparison.OrdinalIgnoreCase)));
        // Once the player has selected a visibly connected route marker, the
        // detail overlay can replace the map title. Do not discard the pending
        // confirmation merely because OCR no longer sees the old heading.
        if (!isRouteScreen && !_routeNodeSelected)
            _routeConfirmationAttempts = 0;

        // "Enter Trail" may be visible but disabled until a connected route node is chosen.
        // Locate a circular map marker in screenshot pixels, without coordinates, DOM or map state.
        if (isRouteScreen && !_routeNodeSelected)
        {
            var node = RouteCircleDetector.Detect(observation.PngBytes);
            if (node is null)
            {
                LastEvidence = new AgentEvidence("route", "No high-confidence circular route node visible; waiting", 0, names);
                return new GameAction(GameActionKind.Wait, DurationMs: 700);
            }

            _routeNodeSelected = true;
            _routeConfirmationAttempts = 0;
            _settling = true;
            _previousFrame = observation.PngBytes;
            LastEvidence = new AgentEvidence("route", "Selecting circular route marker from visible pixels", node.Confidence, names);
            return new GameAction(GameActionKind.Click, X: node.X, Y: node.Y);
        }

        if (_routeNodeSelected)
        {
            if (_routeConfirmationAttempts >= 3)
            {
                LastEvidence = new AgentEvidence("blocked", "Route did not advance after three visual confirmation attempts", 0, names);
                return null;
            }

            // OCR is often unreliable over bright, filled controls. The button must visibly be enabled.
            var confirmation = FilledActionButtonDetector.Detect(observation.PngBytes);
            if (confirmation is null)
            {
                if (!isRouteScreen)
                {
                    // The pending route vanished and no enabled footer exists;
                    // fail closed instead of clicking an unrelated new screen.
                    _routeNodeSelected = false;
                    _routeConfirmationAttempts = 0;
                }
                LastEvidence = new AgentEvidence("route", "Waiting for a visibly enabled route confirmation button", 0, names);
                return new GameAction(GameActionKind.Wait, DurationMs: 550);
            }

            _routeConfirmationAttempts++;
            _settling = true;
            _previousFrame = observation.PngBytes;
            LastEvidence = new AgentEvidence("route", "Activating visually enabled lower action button", confirmation.Confidence, names);
            return new GameAction(GameActionKind.Click, X: confirmation.X, Y: confirmation.Y);
        }

        var match = MenuTextMatcher.FindBest(labels, profile);
        if (match is not null)
        {
            _menuClicks++;
            _emptyScans = 0;
            _explorationSteps = 0;
            _settling = true;
            _previousFrame = observation.PngBytes;
            LastEvidence = new AgentEvidence("menu", "Selecting visible label: " + match.Text, match.Confidence / 100f, names);
            return new GameAction(GameActionKind.Click, X: match.CenterX, Y: match.CenterY);
        }

        _emptyScans++;
        var gameplayCue = labels.Any(t => t.Confidence >= profile.MinimumTextConfidence &&
            profile.GameplayCues.Any(cue => t.Text.Contains(cue, StringComparison.OrdinalIgnoreCase))) ||
            (profile.GameplayCues.Any(cue => string.Equals(cue, "HP", StringComparison.OrdinalIgnoreCase)) &&
                GameplayHudWitness.IsVisible(labels, observation.Width, observation.Height));

        // After a real title click, an otherwise unrecognized town may offer a
        // visual entrance without OCR text. This is a ONE-SHOT uncertain pixel
        // proposal, enabled by the external game profile, never a known gate.
        if (!gameplayCue && _menuClicks > 0 && _explorationSteps == 0 &&
            !_entryProbeTried && profile.EnableIlluminatedEntryProbe)
        {
            var entrance = IlluminatedEntryDetector.Detect(observation.PngBytes);
            if (entrance is not null)
            {
                _entryProbeTried = true;
                _settling = true;
                _previousFrame = observation.PngBytes;
                LastEvidence = new AgentEvidence("entry-probe",
                    "Trying one unverified illuminated focal affordance from screenshot pixels",
                    entrance.Confidence, names);
                return new GameAction(GameActionKind.Click, X: entrance.X, Y: entrance.Y);
            }
        }

        // No guessed clicks or movement before there is visible evidence of gameplay or a preceding menu transition.
        if (!gameplayCue && _menuClicks == 0 && _explorationSteps == 0)
        {
            LastEvidence = new AgentEvidence("unrecognized", "No actionable label or visible gameplay cue; waiting safely", 0, names);
            return new GameAction(GameActionKind.Wait, DurationMs: 700);
        }

        if (!gameplayCue && _explorationSteps == 0)
        {
            // A completed menu click is not evidence that gameplay began. A redesigned
            // title or town screen may still be waiting for a second visible action.
            LastEvidence = new AgentEvidence("transition", "Waiting for confirmed visible gameplay HUD or menu action", 0.3f, names);
            return new GameAction(GameActionKind.Wait, DurationMs: 550);
        }

        // A vanished screenshot HUD can indicate an opaque upgrade/menu overlay.
        // Never keep steering from a stale player-color candidate while OCR is deferred.
        // Profiles without an explicitly configured HUD witness behave unchanged.
        if (profile.CombatHudBar is { } bar && (_explorationSteps > 0 || gameplayCue) &&
            !VisualHudBarWitness.IsVisible(observation.PngBytes, bar))
        {
            _trackedPlayer = null;
            _missedPlayerFrames = 0;
            _previousCombatFrame = null;
            _previousCombatObservation = null;
            _previousFrame = observation.PngBytes;
            _explorationSteps = 0; // Force full OCR next step; do not guess a menu action.
            LastEvidence = new AgentEvidence("hud-hidden",
                "Expected visible combat status bar disappeared; waiting for fresh screenshot and text", 0, names);
            return new GameAction(GameActionKind.Wait, DurationMs: 350);
        }

        // Missing screenshot HUD indicates a possible full-screen upgrade or menu.
        // Block stale combat movement and force fresh OCR on the next screenshot.
        if (_explorationSteps > 0 && profile.CombatHudBar is { } bar &&
            !VisualHudBarWitness.IsVisible(observation.PngBytes, bar))
        {
            _trackedPlayer = null;
            _missedPlayerFrames = 0;
            _previousCombatFrame = null;
            _previousCombatObservation = null;
            _previousFrame = observation.PngBytes;
            _explorationSteps = 0;
            LastEvidence = new AgentEvidence("hud-hidden",
                "Expected combat status bar disappeared; waiting for a new visual screen check", 0, names);
            return new GameAction(GameActionKind.Wait, DurationMs: 350);
        }

        var combat = profile.CombatTracking is null ? null : CombatPixelPerception.Observe(observation.PngBytes,
            profile.CombatTracking, _trackedPlayer);
        if (combat?.Player is { } currentPlayer)
        {
            _trackedPlayer = currentPlayer;
            _missedPlayerFrames = 0;
        }
        else if (++_missedPlayerFrames >= 5)
        {
            _trackedPlayer = null;
        }
        // Purely diagnostic: never allow unvalidated scene-relative motion to drive steering.
        var relativeMotion = _previousCombatFrame is not null && _previousCombatObservation is not null && combat is not null
            ? ThreatMotionAnalyzer.Analyze(_previousCombatObservation, combat,
                VisualSceneMotion.Estimate(_previousCombatFrame, observation.PngBytes))
            : null;
        _previousCombatFrame = combat is null ? null : observation.PngBytes;
        _previousCombatObservation = combat;

        var motion = _previousFrame is null ? (Change: 0f, Quadrants: new float[4]) : VisualMotion.Measure(_previousFrame, observation.PngBytes);
        _previousFrame = observation.PngBytes;
        var avoidance = combat is null ? null : VisualCombatSteering.SelectEvasionDirection(combat,
            profile.CombatTracking!.ThreatAvoidanceRadius);
        var direction = avoidance ?? VisualMotion.ChooseDirection(motion.Quadrants, _explorationSteps);
        var key = profile.MovementKeys[direction];
        _explorationSteps++;

        var source = avoidance is not null ? "candidate-avoidance" : "screen-motion";
        var screen = combat?.Player is { } player
            ? new CombatScreenEvidence(player.X, player.Y, combat.ThreatCandidates.Count, combat.NearestThreatDistance, source)
            {
                BackgroundShiftX = relativeMotion?.Background?.X,
                BackgroundShiftY = relativeMotion?.Background?.Y,
                BackgroundShiftConfidence = relativeMotion?.Background?.Confidence,
                MotionMatchedCandidates = relativeMotion?.Matched ?? 0,
                MotionMovingCandidates = relativeMotion?.Moving ?? 0,
                MotionStationaryCandidates = relativeMotion?.Stationary ?? 0,
                ThreatLocations = combat!.ThreatCandidates.Select(t => new VisualCandidatePoint(t.X, t.Y)).ToArray()
            }
            : null;
        var reason = screen is null ? "No confidently located player; " + source : $"Visual player ({screen.PlayerX:0},{screen.PlayerY:0}); {source}";
        LastEvidence = new AgentEvidence("exploring", reason + "; holding " + key + (scanText ? "" : "; text scan deferred"),
            screen is null ? 0.2f : MathF.Min(0.65f, combat!.Player!.Confidence), names, motion.Change, screen);
        return new GameAction(GameActionKind.HoldKey, Key: key, DurationMs: profile.MovementMilliseconds);
    }
}
