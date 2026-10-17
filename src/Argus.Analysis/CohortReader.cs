using System.Text.Json;
using Argus.Core;

namespace Argus.Analysis;

/// <summary>Offline-only reader of existing ARGUS results; does not connect to games or interpret hidden game state.</summary>
public static class CohortReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<CohortReport> ReadAsync(string directory, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(directory);
        if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException(fullPath);

        var files = Directory.EnumerateFiles(fullPath, "summary.json", SearchOption.AllDirectories)
            .OrderBy(file => file, StringComparer.Ordinal).Take(10001).ToArray();
        if (files.Length == 0) throw new InvalidDataException("No ARGUS summary.json files found.");
        if (files.Length > 10000) throw new InvalidDataException("Too many runs; analyze smaller bounded cohorts.");

        var runs = new List<CohortRun>();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = Path.GetDirectoryName(file) ?? throw new InvalidDataException("Missing run folder.");
            var id = Path.GetFileName(folder);
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("Missing run identifier.");

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(file, cancellationToken));
            Validate(document.RootElement, id);

            var evidence = document.RootElement.Deserialize<RunEvidenceSummary>(JsonOptions)
                ?? throw new InvalidDataException($"Cannot deserialize run '{id}'.");
            RunMetadata metadata = new();
            var metadataFile = Path.Combine(folder, "experiment.json");
            if (File.Exists(metadataFile))
            {
                metadata = JsonSerializer.Deserialize<RunMetadata>(await File.ReadAllTextAsync(metadataFile, cancellationToken),
                    JsonOptions) ?? throw new InvalidDataException($"Invalid experiment.json for '{id}'.");
            }
            runs.Add(new CohortRun(id, evidence, metadata));
        }
        return CohortStatistics.Summarize(runs);
    }

    private static void Validate(JsonElement json, string id)
    {
        if (json.ValueKind != JsonValueKind.Object) throw new InvalidDataException($"Invalid JSON object for '{id}'.");
        string[] required = ["frames", "menuClicks", "routeClicks", "upgradeClicks", "playerLocatedFrames",
            "candidateAvoidanceFrames", "missingPlayerExplorationFrames", "terminalStops", "distinctMovementKeys"];
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var field in required)
            values[field] = Nonnegative(json, field, id, required: true);

        var camera = Nonnegative(json, "cameraEstimatedFrames", id, required: false);
        var moving = Nonnegative(json, "framesWithMovingCandidates", id, required: false);
        var stationary = Nonnegative(json, "framesWithStationaryCandidates", id, required: false);
        if (!json.TryGetProperty("meetsVisualCombatGate", out var gate) ||
            gate.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException($"Run '{id}' is missing a valid visual-gate bool.");

        var frames = values["frames"];
        if (frames < 1 || required.Skip(1).Any(field => values[field] > frames) ||
            camera > values["playerLocatedFrames"] || moving > camera || stationary > camera ||
            values["candidateAvoidanceFrames"] > values["playerLocatedFrames"] ||
            values["playerLocatedFrames"] + values["missingPlayerExplorationFrames"] > frames)
            throw new InvalidDataException($"Run '{id}' has inconsistent frame counts.");

        // An exported gate flag is evidence from the original run, not a newly inferred win/loss.
        var shouldPass = values["menuClicks"] > 0 && values["routeClicks"] >= 2 && values["playerLocatedFrames"] >= 3;
        if (gate.GetBoolean() != shouldPass) throw new InvalidDataException($"Run '{id}' reports an inconsistent visual gate.");
    }

    private static int Nonnegative(JsonElement json, string property, string id, bool required)
    {
        if (!json.TryGetProperty(property, out var value))
        {
            if (!required) return 0;
            throw new InvalidDataException($"Run '{id}' is missing '{property}'.");
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number) || number < 0)
            throw new InvalidDataException($"Run '{id}' has an invalid '{property}'.");
        return number;
    }
}
