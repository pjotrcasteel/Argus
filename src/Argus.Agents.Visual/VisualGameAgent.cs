using Argus.Core;

namespace Argus.Agents.Visual;

/// <summary>
/// First black-box baseline: OCR-driven menus, then experimental motion-biased exploration.
/// This is a heuristic, NOT a trained policy or evidence of competent game completion.
/// </summary>
public sealed class VisualGameAgent(IFrameTextReader textReader, VisualGameProfile profile) : IVisualAgent, IAgentEvidenceProvider
{
    private byte[]? _previousFrame;
    private int _menuClicks;
    private int _emptyScans;
    private int _explorationSteps;
    private bool _routeNodeSelected;
    private bool _settling;

    public AgentEvidence? LastEvidence { get; private set; }

    public async ValueTask<GameAction?> DecideAsync(FrameObservation observation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_settling)
        {
            _settling = false;
            LastEvidence = new AgentEvidence("transition", "Allowing visual transition to finish", 1, []);
            _previousFrame = observation.PngBytes;
            return new GameAction(GameActionKind.Wait, DurationMs: 650);
        }

        var labels = await textReader.ReadAsync(observation, cancellationToken);
        var names = labels.Select(t => t.Text).Take(24).ToArray();
        var isRouteScreen = labels.Any(t => t.Confidence >= profile.MinimumTextConfidence &&
            profile.RouteCues.Any(cue => t.Text.Contains(cue, StringComparison.OrdinalIgnoreCase)));
        if (!isRouteScreen) _routeNodeSelected = false;

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
            _settling = true;
            _previousFrame = observation.PngBytes;
            LastEvidence = new AgentEvidence("route", "Selecting circular route marker from visible pixels", node.Confidence, names);
            return new GameAction(GameActionKind.Click, X: node.X, Y: node.Y);
        }

        if (isRouteScreen && _routeNodeSelected)
        {
            // OCR is often unreliable over bright, filled controls. The button must visibly be enabled.
            var confirmation = FilledActionButtonDetector.Detect(observation.PngBytes);
            if (confirmation is null)
            {
                LastEvidence = new AgentEvidence("route", "Waiting for a visibly enabled route confirmation button", 0, names);
                return new GameAction(GameActionKind.Wait, DurationMs: 550);
            }

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
            profile.GameplayCues.Any(cue => t.Text.Contains(cue, StringComparison.OrdinalIgnoreCase)));

        // No guessed clicks or movement before there is visible evidence of gameplay or a preceding menu transition.
        if (!gameplayCue && _menuClicks == 0 && _explorationSteps == 0)
        {
            LastEvidence = new AgentEvidence("unrecognized", "No actionable label or visible gameplay cue; waiting safely", 0, names);
            return new GameAction(GameActionKind.Wait, DurationMs: 700);
        }

        if (!gameplayCue && _explorationSteps == 0 && _emptyScans < 3)
        {
            LastEvidence = new AgentEvidence("transition", "Checking screen after a visible menu selection", 0.3f, names);
            return new GameAction(GameActionKind.Wait, DurationMs: 550);
        }

        var motion = _previousFrame is null ? (Change: 0f, Quadrants: new float[4]) : VisualMotion.Measure(_previousFrame, observation.PngBytes);
        _previousFrame = observation.PngBytes;
        var direction = VisualMotion.ChooseDirection(motion.Quadrants, _explorationSteps);
        var key = profile.MovementKeys[direction];
        _explorationSteps++;

        LastEvidence = new AgentEvidence("exploring", "Experimental screen-motion steering; holding " + key, 0.2f, names, motion.Change);
        return new GameAction(GameActionKind.HoldKey, Key: key, DurationMs: profile.MovementMilliseconds);
    }
}
