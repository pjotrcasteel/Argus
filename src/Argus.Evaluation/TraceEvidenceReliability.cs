using System.Text.Json;

namespace Argus.Evaluation;

/// <summary>Guards measurements that predate the M2.5a final-frame observation fix.</summary>
public static class TraceEvidenceReliability
{
    public static bool IsUnverifiedLegacyFinal(JsonElement record, int step, int frameCount)
    {
        if (step != frameCount - 1 || record.ValueKind != JsonValueKind.Object ||
            record.TryGetProperty("inputSuppressedAtStepLimit", out _) ||
            !record.TryGetProperty("action", out var action) || action.ValueKind != JsonValueKind.Null ||
            !record.TryGetProperty("evidence", out var evidence) || evidence.ValueKind != JsonValueKind.Object ||
            !evidence.TryGetProperty("phase", out var phase) || phase.ValueKind != JsonValueKind.String)
            return false;

        // Older runners skipped DecideAsync at the final bounded step, so LastEvidence was
        // copied from the previous image. A terminal phase is a real decision, not this bug.
        return phase.GetString() == "exploring";
    }
}
