using System.Security.Cryptography;
using System.Text.Json;
using Argus.Evaluation;
using Argus.Replay;

if (args.Length != 6 || args[0] != "--runs" || args[2] != "--seed" || args[4] != "--samples" ||
    !int.TryParse(args[5], out var sampleCount))
{
    Console.Error.WriteLine("Usage: dotnet run --project src/Argus.Study.Tool -- --runs results/experiments --seed experiment-1 --samples 12");
    return 2;
}

try
{
    var root = Path.GetFullPath(args[1]);
    if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
    var folders = Directory.EnumerateDirectories(root).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    if (folders.Length is < 2 or > 128)
        throw new InvalidDataException("Use 2–128 independent run subdirectories in the chosen folder.");

    var runs = new List<StudyRun>();
    var seenTraces = new HashSet<string>(StringComparer.Ordinal);
    foreach (var folder in folders)
    {
        if (new DirectoryInfo(folder).Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidDataException("Symlinked run folders are not allowed.");
        var trace = await ReplayTraceReader.ReadAsync(folder);
        await using var raw = File.OpenRead(Path.Combine(folder, "trace.jsonl"));
        var fingerprint = Convert.ToHexString(await SHA256.HashDataAsync(raw));
        if (!seenTraces.Add(fingerprint))
            throw new InvalidDataException("Identical traces do not count as independent playtest sessions.");
        runs.Add(new StudyRun(Path.GetFileName(folder), trace));
    }

    var study = StudyPlanner.Create(runs, args[3], sampleCount);
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
    foreach (var assignment in study.Assignments)
    {
        var folder = Path.Combine(root, assignment.RunId);
        var replay = runs.Single(r => r.RunId == assignment.RunId).Trace;
        var labels = StudyPlanner.Template(assignment, replay);
        GroundTruth.Validate(labels, assignment.RunId, replay);
        await File.WriteAllTextAsync(Path.Combine(folder, "blind-labels.template.json"), JsonSerializer.Serialize(labels, options));
        await File.WriteAllTextAsync(Path.Combine(folder, "blind-annotate.html"), AnnotationPageBuilder.Render(replay, labels, blind: true));
    }

    var planFile = Path.Combine(root, "study-plan.json");
    await File.WriteAllTextAsync(planFile, JsonSerializer.Serialize(study, options));
    var development = study.Assignments.Count(x => x.Partition == "development");
    var heldOut = study.Assignments.Count(x => x.Partition == "held-out");
    Console.WriteLine($"ARGUS blinded study created: {development} development runs, {heldOut} held-out runs.");
    Console.WriteLine("Every label begins UNKNOWN. No accuracy claims are possible without independently reviewing the screenshots.");
    Console.WriteLine($"Local plan: {planFile}");
    return 0;
}
catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or JsonException
    or ArgumentException or ArgumentOutOfRangeException)
{
    Console.Error.WriteLine("ARGUS blind study failed: " + ex.Message);
    return 1;
}
