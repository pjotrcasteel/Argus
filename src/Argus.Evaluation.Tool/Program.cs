using System.Text.Json;
using Argus.Evaluation;
using Argus.Replay;

if (args.Length != 4 || args[0] != "--run" || args[2] is not ("--template" or "--labels" or "--annotate"))
{
    Console.Error.WriteLine("Usage: dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --template results/my-run/labels.template.json");
    Console.Error.WriteLine("   or: dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --labels results/my-run/labels.json");
    Console.Error.WriteLine("   or: dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --annotate results/my-run/annotate.html");
    return 2;
}

try
{
    var run = Path.GetFullPath(args[1]);
    var replay = await ReplayTraceReader.ReadAsync(run);
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

    if (args[2] == "--template")
    {
        var target = Path.GetFullPath(args[3]);
        if (!string.Equals(Path.GetDirectoryName(target), run, StringComparison.Ordinal))
            throw new InvalidDataException("Template output must remain inside the selected run folder.");
        var template = GroundTruth.CreateTemplate(run, replay);
        await File.WriteAllTextAsync(target, JsonSerializer.Serialize(template, options));
        Console.WriteLine($"Created {template.Frames.Count} unannotated sample frames: {target}");
        Console.WriteLine("Every label is UNKNOWN. Review actual screenshots before scoring.");
    }
    else if (args[2] == "--annotate")
    {
        var destination = Path.GetFullPath(args[3]);
        if (!string.Equals(Path.GetDirectoryName(destination), run, StringComparison.Ordinal) ||
            !string.Equals(Path.GetFileName(destination), "annotate.html", StringComparison.Ordinal))
            throw new InvalidDataException("Annotation page output must be annotate.html inside the selected run folder.");
        var template = GroundTruth.CreateTemplate(run, replay);
        await File.WriteAllTextAsync(destination, AnnotationPageBuilder.Render(replay, template));
        Console.WriteLine($"Created standalone annotation page for {template.Frames.Count} real screenshots: {destination}");
        Console.WriteLine("All human labels start UNKNOWN; open annotate.html locally to add labels and download labels.json.");
    }
    else
    {
        var labels = await GroundTruth.ReadAsync(args[3], run, replay);
        var report = await PerceptionScoring.ScoreAsync(run, labels, replay);
        var jsonPath = Path.Combine(run, "perception-report.json");
        var markdownPath = Path.Combine(run, "perception-report.md");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, options));
        await File.WriteAllTextAsync(markdownPath, PerceptionScoring.RenderMarkdown(report));
        Console.WriteLine($"Scored {report.LabeledFrames} labeled frames. Threat evidence unavailable in {report.ThreatEvidenceUnavailableFrames} labeled frames.");
        Console.WriteLine($"Report: {markdownPath}");
    }
    return 0;
}
catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or JsonException or ArgumentOutOfRangeException)
{
    Console.Error.WriteLine("ARGUS perception evaluation failed: " + ex.Message);
    return 1;
}
