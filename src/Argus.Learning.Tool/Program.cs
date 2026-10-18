using System.Text.Json;
using Argus.Evaluation;

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

if (args is ["--spatial-archive", var archive, "--labels-f", var f, "--labels-g", var g,
        "--labels-h", var h, "--output", var output])
{
    try
    {
        // Originals are read from the verified development-only screenshot artifact.
        // Human exports and fitted weights are never sent to public workflows or GitHub Pages.
        var rows = SpatialProbeImporter.ReadPrivate(archive, [f, g, h]);
        var study = SpatialProbeLearning.Evaluate(rows);
        var privateFolder = Path.GetFullPath(output);
        Directory.CreateDirectory(privateFolder);
        await File.WriteAllTextAsync(Path.Combine(privateFolder, "spatial-probe-learning.json"),
            JsonSerializer.Serialize(study, json));
        await File.WriteAllTextAsync(Path.Combine(privateFolder, "spatial-probe-learning.md"),
            SpatialProbeLearning.RenderMarkdown(study));
        Console.WriteLine($"ARGUS M2.16: {study.Status}; {study.Labeled} reviewed local probes; offline only.");
        foreach (var blocker in study.Blockers) Console.WriteLine("NOT_READY: " + blocker);
        Console.WriteLine("Outputs are PRIVATE: do not commit weights, human labels or source research bundles.");
        return 0;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
        or ArgumentException or JsonException or InvalidOperationException or KeyNotFoundException)
    {
        Console.Error.WriteLine("M2.16 private spatial study rejected input: " + ex.Message);
        return 1;
    }
}

if (args.Length != 2 || args[0] != "--runs")
{
    Console.Error.WriteLine("Usage: dotnet run --project src/Argus.Learning.Tool -- --runs PRIVATE_DEVELOPMENT_ROOT");
    Console.Error.WriteLine("Or: --spatial-archive M215_ORIGINAL_REVIEW.zip --labels-f F.json --labels-g G.json " +
        "--labels-h H.json --output PRIVATE_DIRECTORY");
    Console.Error.WriteLine("Reads ONLY dev-* evidence; original held-out run-01 must stay untouched.");
    return 2;
}

try
{
    var root = Path.GetFullPath(args[1]);
    var rows = PlayerShadowLearningData.ReadDevelopment(root);
    var result = PlayerShadowOfflineLearning.Evaluate(rows);
    await File.WriteAllTextAsync(Path.Combine(root, "offline-player-learning.json"),
        JsonSerializer.Serialize(result, json));
    await File.WriteAllTextAsync(Path.Combine(root, "offline-player-learning.md"),
        PlayerShadowOfflineLearning.RenderMarkdown(result));

    Console.WriteLine($"ARGUS offline player learning: {result.Status}; " +
        $"{result.DevelopmentRuns} development run groups, {result.TotalLabeled} labeled feature rows (origin must be verified).");
    foreach (var blocker in result.Blockers) Console.WriteLine("NOT_READY: " + blocker);
    if (result.Trained)
        foreach (var fold in result.LeaveOneRunOut)
            Console.WriteLine($"Left out {fold.HeldOutRunId}: TP {fold.ModelTruePositive}, " +
                $"FP {fold.ModelFalsePositive}, FN {fold.ModelFalseNegative}, TN {fold.ModelTrueNegative}; " +
                "never a live game player.");
    Console.WriteLine("Only local offline research files written. Human image labels are never uploaded.");
    return 0;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
    or ArgumentException or JsonException)
{
    Console.Error.WriteLine("ARGUS offline learning rejected input: " + ex.Message);
    return 1;
}
