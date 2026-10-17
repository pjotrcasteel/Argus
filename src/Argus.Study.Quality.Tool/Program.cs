using System.Text.Json;
using Argus.Evaluation;

if (args.Length is not (2 or 3) || args[0] != "--runs" ||
    args.Length == 3 && args[2] != "--appearance-readiness")
{
    Console.Error.WriteLine("Usage: dotnet run --project src/Argus.Study.Quality.Tool -- --runs results/experiments");
    Console.Error.WriteLine("Optional: --appearance-readiness after --runs requires per-development-run appearance-audit.json and real human labels.");
    Console.Error.WriteLine("First generate study-plan.json and blind-annotate.html with Argus.Study.Tool.");
    return 2;
}

try
{
    var root = Path.GetFullPath(args[1]);
    var quality = await StudyQualityEvaluation.EvaluateAsync(root);
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
    var jsonPath = Path.Combine(root, "study-quality.json");
    var markdownPath = Path.Combine(root, "study-quality.md");
    await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(quality, options));
    await File.WriteAllTextAsync(markdownPath, StudyQualityEvaluation.RenderMarkdown(quality));

    Console.WriteLine($"ARGUS blind study status: {quality.Status}; no human labels are created by this tool.");
    foreach (var run in quality.Runs)
        Console.WriteLine($"{run.RunId} [{run.Partition}]: {run.Status}; " +
            $"human reviewed player {run.HumanReviewedPlayerFrames}/{run.SelectedFrames}, " +
            $"threats {run.HumanReviewedThreatFrames}/{run.SelectedFrames}.");
    Console.WriteLine($"Research files: {jsonPath} and {markdownPath}.");
    if (args.Length == 3)
    {
        var development = new List<AppearanceAuditReport>();
        foreach (var run in quality.Runs.Where(r => r.Partition == "development"))
        {
            if (run.Perception is null)
                throw new InvalidDataException("Appearance readiness needs genuine human review for " + run.RunId);
            var file = Path.Combine(root, run.RunId, "appearance-audit.json");
            var audit = JsonSerializer.Deserialize<AppearanceAuditReport>(await File.ReadAllTextAsync(file), options)
                ?? throw new InvalidDataException("Invalid appearance audit: " + run.RunId);
            if (audit.RunId != run.RunId || audit.LabeledFrames != run.SelectedFrames)
                throw new InvalidDataException("Appearance audit is inconsistent with study plan: " + run.RunId);
            development.Add(audit);
        }

        var readiness = AppearanceReadiness.Assess(development);
        await File.WriteAllTextAsync(Path.Combine(root, "appearance-readiness.json"),
            JsonSerializer.Serialize(readiness, options));
        await File.WriteAllTextAsync(Path.Combine(root, "appearance-readiness.md"),
            AppearanceReadiness.RenderMarkdown(readiness));
        Console.WriteLine($"Appearance data readiness: {readiness.Status}; " +
            $"{readiness.DevelopmentRuns} independent development run identities, " +
            $"{readiness.UnmatchedCandidateCenters} unmatched candidate examples. " +
            "Never infer classifier accuracy from this gate.");
    }
    return 0;
}
catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or JsonException
    or ArgumentException)
{
    Console.Error.WriteLine("ARGUS blind study evaluation failed: " + ex.Message);
    return 1;
}
