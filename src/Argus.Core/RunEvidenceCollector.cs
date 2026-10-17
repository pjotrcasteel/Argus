namespace Argus.Core;

/// <summary>
/// Counts observable black-box evidence only. Passing a gate does not prove intelligent or humanlike play.
/// </summary>
public sealed record RunEvidenceSummary(
    int Frames,
    int MenuClicks,
    int RouteClicks,
    int UpgradeClicks,
    int PlayerLocatedFrames,
    int CandidateAvoidanceFrames,
    int MissingPlayerExplorationFrames,
    int TerminalStops,
    int DistinctMovementKeys,
    bool MeetsVisualCombatGate);

public sealed class RunEvidenceCollector
{
    private int _frames;
    private int _menu;
    private int _route;
    private int _upgrades;
    private int _playerLocated;
    private int _avoiding;
    private int _missingPlayer;
    private int _terminal;
    private readonly HashSet<string> _movementKeys = new(StringComparer.OrdinalIgnoreCase);

    public void Record(GameAction? action, AgentEvidence? evidence)
    {
        _frames++;
        if (evidence is null) return;

        if (action?.Kind == GameActionKind.Click)
        {
            switch (evidence.Phase)
            {
                case "menu": _menu++; break;
                case "route": _route++; break;
                case "choice": _upgrades++; break;
            }
        }

        if (evidence.Combat is not null)
        {
            _playerLocated++;
            if (evidence.Combat.SteeringSource == "candidate-avoidance") _avoiding++;
        }
        else if (evidence.Phase == "exploring")
        {
            _missingPlayer++;
        }

        if (evidence.Phase == "terminal" && action is null) _terminal++;
        if (action?.Kind == GameActionKind.HoldKey && !string.IsNullOrWhiteSpace(action.Key))
            _movementKeys.Add(action.Key);
    }

    public RunEvidenceSummary Snapshot()
    {
        // A successful transport test must have reached visual combat; mere page loading isn't enough.
        var passed = _menu > 0 && _route >= 2 && _playerLocated >= 3;
        return new RunEvidenceSummary(_frames, _menu, _route, _upgrades, _playerLocated, _avoiding,
            _missingPlayer, _terminal, _movementKeys.Count, passed);
    }
}
