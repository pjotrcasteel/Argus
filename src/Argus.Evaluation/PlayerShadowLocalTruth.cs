namespace Argus.Evaluation;

/// <summary>
/// Label for a *specific stale pixel location*, not global player visibility.
/// The 26px matching radius is preregistered and mirrors the original M2.10
/// human-proposal matching criterion. Does not affect live game inference.
/// </summary>
public static class PlayerShadowLocalTruth
{
    public const float MatchRadiusPixels = 26;

    public static int? ReviewedLocalLabel(PlayerCurrentFrameVetoEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.HumanReference == "VISIBLE")
        {
            if (evidence.HumanDistancePixels is null ||
                !float.IsFinite(evidence.HumanDistancePixels.Value) ||
                evidence.HumanDistancePixels.Value < 0 ||
                evidence.HumanDistancePixels.Value > 2000)
                throw new InvalidDataException("A human-visible bridge must include a valid original measured distance.");

            return evidence.HumanDistancePixels.Value <= MatchRadiusPixels ? 1 : 0;
        }

        if (evidence.HumanDistancePixels is not null)
            throw new InvalidDataException("Only visible-player human truth may carry a measured player distance.");
        return evidence.HumanReference switch
        {
            "ABSENT" => 0,
            "UNKNOWN" or "UNREVIEWED" => null,
            _ => throw new InvalidDataException("Unknown human reference status.")
        };
    }
}
