using System.Text.Json;
using Argus.Core;
using Argus.Evaluation;
using Argus.Replay;

if (args.Length != 4 || args[0] != "--run" || args[2] is not ("--template" or "--labels" or "--annotate" or "--blind-annotate" or "--shadow-edges" or "--appearance-audit"))
{
    Console.Error.WriteLine("Usage: dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --template results/my-run/labels.template.json");
    Console.Error.WriteLine("   or: dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --annotate results/my-run/annotate.html");
    Console.Error.WriteLine("   or: dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --labels results/my-run/labels.json");
    Console.Error.WriteLine("   or: dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --blind-annotate results/my-run/blind-annotate.html");
    Console.Error.WriteLine("   or: dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --shadow-edges examples/wyrmforge.profile.json");
    Console.Error.WriteLine("   or: dotnet run --project src/Argus.Evaluation.Tool -- --run results/my-run --appearance-audit examples/wyrmforge.profile.json");
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
    else if (args[2] is "--annotate" or "--blind-annotate")
    {
        var target = Path.GetFullPath(args[3]);
        var blind = args[2] == "--blind-annotate";
        var expectedName = blind ? "blind-annotate.html" : "annotate.html";
        if (!string.Equals(Path.GetDirectoryName(target), run, StringComparison.Ordinal) ||
            !string.Equals(Path.GetFileName(target), expectedName, StringComparison.Ordinal))
            throw new InvalidDataException("Annotation UI output must use its expected HTML filename inside the selected run folder.");
        var template = GroundTruth.CreateTemplate(run, replay);
        await File.WriteAllTextAsync(target, AnnotationPageBuilder.Render(replay, template, blind));
        Console.WriteLine($"Created offline annotation UI from {template.Frames.Count} real recorded frames: {target}");
        Console.WriteLine(blind
            ? "Blind mode: agent predictions/explanations are removed. Label only visible screenshot evidence."
            : "Open the HTML locally, mark visible objects, download labels.json, then evaluate it offline.");
    }
    else if (args[2] == "--appearance-audit")
    {
        var labels = await GroundTruth.ReadAsync(Path.Combine(run, "labels.json"), run, replay);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
        var profile = JsonSerializer.Deserialize<VisualGameProfile>(await File.ReadAllTextAsync(args[3]), json)
            ?? throw new InvalidDataException("Invalid visual game profile.");
        profile.Validate();
        if (profile.CombatTracking is null)
            throw new InvalidDataException("Appearance analysis requires combat tracking settings.");

        var audit = await AppearanceAudit.AnalyzeAsync(run, labels, replay, profile.CombatTracking);
        var output = Path.Combine(run, "appearance-audit.json");
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(audit, options));
        await File.WriteAllTextAsync(Path.Combine(run, "appearance-audit.md"), AppearanceAudit.RenderMarkdown(audit));
        Console.WriteLine($"Appearance audit: {audit.ScoredFrames}/{audit.LabeledFrames} comparable reviewed frames, " +
            $"{audit.Candidates.Count(c => c.MatchedHuman)} matched candidates and " +
            $"{audit.Candidates.Count(c => !c.MatchedHuman)} unmatched candidates.");
        Console.WriteLine($"Saved local feature audit: {output}");
    }
    else if (args[2] == "--shadow-edges")
    {
        var labels = await GroundTruth.ReadAsync(Path.Combine(run, "labels.json"), run, replay);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
        var profile = JsonSerializer.Deserialize<VisualGameProfile>(await File.ReadAllTextAsync(args[3]), json)
            ?? throw new InvalidDataException("Invalid visual game profile.");
        profile.Validate();
        if (profile.CombatTracking is null)
            throw new InvalidDataException("Shadow scan requires a combat tracking profile.");

        var study = await ShadowEdgeStudy.CompareAsync(run, labels, replay, profile.CombatTracking);
        var output = Path.Combine(run, "shadow-edge-study.json");
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(study, options));
        await File.WriteAllTextAsync(Path.Combine(run, "shadow-edge-study.md"), ShadowEdgeStudy.RenderMarkdown(study));
        Console.WriteLine($"Shadow A/B: {study.ScoredFrames} comparable frames, " +
            $"baseline TP/FP/FN {study.Baseline.TruePositive}/{study.Baseline.FalsePositive}/{study.Baseline.FalseNegative}, " +
            $"raw shadow {study.Expanded.TruePositive}/{study.Expanded.FalsePositive}/{study.Expanded.FalseNegative}, " +
            $"moving temporal {study.Temporal?.TruePositive}/{study.Temporal?.FalsePositive}/{study.Temporal?.FalseNegative}, " +
            $"stationary research {study.PersistentStationaryTrial?.TruePositive}/" +
            $"{study.PersistentStationaryTrial?.FalsePositive}/{study.PersistentStationaryTrial?.FalseNegative}.");
        Console.WriteLine($"Saved local study: {output}");
    }
    else
    {
        var labels = await GroundTruth.ReadAsync(args[3], run, replay);
        var report = await PerceptionScoring.ScoreAsync(run, labels, replay);
        var jsonPath = Path.Combine(run, "perception-report.json");
        var markdownPath = Path.Combine(run, "perception-report.md");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, options));
        await File.WriteAllTextAsync(markdownPath, PerceptionScoring.RenderMarkdown(report));
        var failures = await PerceptionFailureAnalysis.AnalyzeAsync(run, labels, replay);
        await File.WriteAllTextAsync(Path.Combine(run, "perception-failures.json"), JsonSerializer.Serialize(failures, options));
        await File.WriteAllTextAsync(Path.Combine(run, "perception-failures.md"), PerceptionFailureAnalysis.RenderMarkdown(failures));
        Console.WriteLine($"Scored {report.LabeledFrames} labeled frames; {failures.ScoredThreatFrames} comparable threat frames.");
        Console.WriteLine($"Unavailable threat frames: {report.ThreatEvidenceUnavailableFrames}; " +
            $"excluded legacy final frames: {report.LegacyFinalUnverifiedFrames}; " +
            $"human-marked threat centers UNAVAILABLE: {report.ThreatCentersOnUnavailableFrames} " +
            $"across {report.ThreatFramesWithHumanCentersAndUnavailableEvidence} frames.");
        Console.WriteLine($"Reports: {markdownPath}, {Path.Combine(run, "perception-failures.md")}");
    }
    return 0;
}
catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or JsonException or ArgumentOutOfRangeException)
{
    Console.Error.WriteLine("ARGUS perception evaluation failed: " + ex.Message);
    return 1;
}
