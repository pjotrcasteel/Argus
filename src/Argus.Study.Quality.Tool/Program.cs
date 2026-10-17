using System.Text.Json;
using Argus.Evaluation;

if (args.Length != 2 || args[0] != "--runs")
{
    Console.Error.WriteLine("Usage: dotnet run --project src/Argus.Study.Quality.Tool -- --runs results/experiments");
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
    return 0;
}
catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or JsonException
    or ArgumentException)
{
    Console.Error.WriteLine("ARGUS blind study evaluation failed: " + ex.Message);
    return 1;
}
