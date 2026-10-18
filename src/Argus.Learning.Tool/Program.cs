using System.Text.Json;
using Argus.Evaluation;

if (args.Length != 2 || args[0] != "--runs")
{
    Console.Error.WriteLine("Usage: dotnet run --project src/Argus.Learning.Tool -- --runs PRIVATE_DEVELOPMENT_ROOT");
    Console.Error.WriteLine("Reads ONLY dev-* child directories and their player-shadow-feature-rows.csv.");
    Console.Error.WriteLine("Never use original run-01 held-out for tuning. No game input.");
    return 2;
}

try
{
    var root = Path.GetFullPath(args[1]);
    var rows = PlayerShadowLearningData.ReadDevelopment(root);
    var result = PlayerShadowOfflineLearning.Evaluate(rows);
    var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
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
