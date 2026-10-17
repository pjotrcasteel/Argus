using System.Security.Cryptography;
using System.Text.Json;
using Argus.Personas;
using Argus.Replay;

if (args.Length != 6 || args[0] != "--runs" || args[2] != "--movement-keys" || args[4] != "--seed" ||
    !int.TryParse(args[5], out var seed))
{
    Console.Error.WriteLine("Usage: dotnet run --project src/Argus.Persona.Cohort.Tool -- --runs results/experiments --movement-keys w,a,s,d --seed 42");
    return 2;
}

try
{
    var root = Path.GetFullPath(args[1]);
    if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
    if (new DirectoryInfo(root).Attributes.HasFlag(FileAttributes.ReparsePoint))
        throw new InvalidDataException("The cohort root must not be a symbolic link.");
    var keyList = args[3].Split(',', StringSplitOptions.TrimEntries);
    if (keyList.Length != 4) throw new InvalidDataException("Specify four distinct up,left,down,right movement keys.");
    var keys = new MovementBindings(keyList[0], keyList[1], keyList[2], keyList[3]);
    keys.Validate();

    var folders = Directory.EnumerateDirectories(root).OrderBy(x => x, StringComparer.Ordinal).Take(129).ToArray();
    if (folders.Length is < 2 or > 128) throw new InvalidDataException("Require 2–128 independent run subdirectories.");
    var fingerprints = new HashSet<string>(StringComparer.Ordinal);
    var sessions = new List<PersonaCohortSession>();
    foreach (var folder in folders)
    {
        if (new DirectoryInfo(folder).Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidDataException("Linked run folders are not supported for local cohort research.");

        var name = Path.GetFileName(folder);
        if (name.Length > 64) throw new InvalidDataException("Run ID exceeds 64 characters.");
        var tracePath = Path.Combine(folder, "trace.jsonl");
        var trace = await ReplayTraceReader.ReadAsync(folder);
        await using var stream = File.OpenRead(tracePath);
        var fingerprint = Convert.ToHexString(await SHA256.HashDataAsync(stream));
        if (!fingerprints.Add(fingerprint))
            throw new InvalidDataException("Identical trace files cannot count as two independent sessions.");

        var context = new PersonaSessionContext();
        var manifest = Path.Combine(folder, "experiment.json");
        if (File.Exists(manifest))
        {
            if (new FileInfo(manifest).Length > 16384) throw new InvalidDataException("Experiment manifest is too large.");
            context = JsonSerializer.Deserialize<PersonaSessionContext>(await File.ReadAllTextAsync(manifest),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) ??
                throw new InvalidDataException("Invalid experiment manifest.");
        }
        sessions.Add(new PersonaCohortSession(name, trace, context));
    }

    var report = PersonaCohortStudy.Analyze(sessions, seed, keys);
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
    var jsonPath = Path.Combine(root, "persona-cohort.json");
    var markdownPath = Path.Combine(root, "persona-cohort.md");
    await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, options));
    await File.WriteAllTextAsync(markdownPath, PersonaCohortMarkdown.Render(report));
    Console.WriteLine($"ARGUS research cohort: {report.RunCount} separate trace folders, seed {seed}.");
    Console.WriteLine($"Matching experiment metadata: {(report.MatchedContext ? "YES" : "UNKNOWN OR DIFFERENT")}.");
    Console.WriteLine($"Reports: {jsonPath} and {markdownPath}. Proposals were never executed.");
    return 0;
}
catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException
    or JsonException or ArgumentException)
{
    Console.Error.WriteLine("ARGUS persona cohort failed: " + ex.Message);
    return 1;
}
