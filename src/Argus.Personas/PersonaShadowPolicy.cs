using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Argus.Core;
using Argus.Replay;

namespace Argus.Personas;

public sealed record PersonaShadowProposal(string PersonaId, GameAction? ProposedAction, string Reason, float EvidenceConfidence);

/// <summary>
/// Propose different actions from identical recorded VISUAL evidence. Never executes input, queries games
/// or claims a real player would behave this way. This is a testable hypothesis for future agent research.
/// </summary>
public static class PersonaShadowPolicy
{
    public static PersonaShadowProposal Propose(ReplayFrame frame, PersonaProfile persona, MovementBindings keys, int seed, string? randomStreamKey = null)
    {
        persona.Validate();
        keys.Validate();
        if (randomStreamKey is { Length: 0 or > 128 }) throw new ArgumentException("Ablation random stream key must be 1–128 characters.", nameof(randomStreamKey));
        var stream = randomStreamKey ?? persona.Id;
        if (frame.Phase != "exploring")
            return new PersonaShadowProposal(persona.Id, null, "No shadow movement outside visually inferred gameplay.", 0);

        var confidence = Math.Clamp(frame.Confidence ?? 0, 0f, 1f);
        var threshold = 0.15f + persona.Attention * 0.55f;
        if (frame.PlayerX is null || frame.PlayerY is null || confidence < threshold)
            return new PersonaShadowProposal(persona.Id, new GameAction(GameActionKind.Wait, DurationMs: 350),
                "Uncertain player location or visual confidence; waiting instead of assuming a safe direction.", confidence);

        var player = new VisualCandidatePoint(frame.PlayerX.Value, frame.PlayerY.Value);
        var nearest = frame.ThreatLocations?
            .OrderBy(t => Distance(t, player)).FirstOrDefault();
        if (nearest is not null && Distance(nearest, player) <= 160)
        {
            var toward = Toward(player, nearest);
            var direction = persona.RiskAppetite switch
            {
                < 0.35f => (toward + 2) % 4,
                > 0.65f => toward,
                _ => ExplorationDirection(frame.Step, persona, seed, stream)
            };
            var rationale = persona.RiskAppetite switch
            {
                < 0.35f => "Moving away from an unverified nearby visual threat candidate.",
                > 0.65f => "Moving toward an unverified nearby visual threat candidate.",
                _ => "Balancing visible candidate proximity with exploration."
            };
            return Movement(persona, keys.At(direction), rationale, confidence);
        }

        // A missing threat list is UNKNOWN evidence, not the assertion that there are no enemies.
        var unknownThreats = frame.ThreatLocations is null;
        var pause = (Stable(seed, stream, frame.Step.ToString(System.Globalization.CultureInfo.InvariantCulture)) % 1000) <
            (uint)(persona.Patience * 220);
        if (pause)
            return new PersonaShadowProposal(persona.Id, new GameAction(GameActionKind.Wait, DurationMs: 300),
                unknownThreats ? "Waiting during uncertain threat visibility." : "Patient observation before exploring.", confidence);

        var index = ExplorationDirection(frame.Step, persona, seed, stream);
        return Movement(persona, keys.At(index),
            unknownThreats ? "Exploration under unknown visual threat coverage." : "Exploration with no nearby detected candidate.",
            confidence);
    }

    private static PersonaShadowProposal Movement(PersonaProfile p, string key, string reason, float confidence)
    {
        var duration = (int)MathF.Round(420 - p.MotorPrecision * 300);
        return new PersonaShadowProposal(p.Id, new GameAction(GameActionKind.HoldKey, Key: key, DurationMs: duration),
            reason + " Shadow-only, not a verified human behavior.", confidence);
    }

    private static int Toward(VisualCandidatePoint player, VisualCandidatePoint candidate)
    {
        var dx = candidate.X - player.X;
        var dy = candidate.Y - player.Y;
        if (MathF.Abs(dx) >= MathF.Abs(dy)) return dx < 0 ? 1 : 3;
        return dy < 0 ? 0 : 2;
    }

    private static int ExplorationDirection(int step, PersonaProfile p, int seed, string stream) =>
        (int)(Stable(seed, stream, p.Exploration >= 0.6f
            ? step.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "consistent") % 4);

    private static float Distance(VisualCandidatePoint a, VisualCandidatePoint b) =>
        MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static uint Stable(int seed, string persona, string frame)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            "\n" + persona + "\n" + frame));
        return BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(0, 4));
    }
}
