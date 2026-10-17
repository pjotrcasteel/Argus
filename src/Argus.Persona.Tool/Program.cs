using System.Text.Json;
using System.Text.Json.Serialization;
using Argus.Personas;
using Argus.Replay;

if (args.Length != 6 || args[0] != "--run" || args[2] != "--movement-keys" || args[4] != "--seed" ||
    !int.TryParse(args[5], out var seed))
{
    Console.Error.WriteLine("Usage: dotnet run --project src/Argus.Persona.Tool -- --run results/my-run --movement-keys w,a,s,d --seed 42");
    return 2;
}

try
{
    var folder = Path.GetFullPath(args[1]);
    var keyList = args[3].Split(',', StringSplitOptions.TrimEntries);
    if (keyList.Length != 4) throw new InvalidDataException("Movement keys need up,left,down,right order.");
    var keys = new MovementBindings(keyList[0], keyList[1], keyList[2], keyList[3]);
    keys.Validate();
    var replay = await ReplayTraceReader.ReadAsync(folder);
    var profiles = PersonaPresets.Default;
    foreach (var profile in profiles) profile.Validate();

    var path = Path.Combine(folder, "persona-shadow.jsonl");
    await using var output = new FileStream(path, FileMode.Create, FileAccess.Write);
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    var proposals = new Dictionary<string, int>(StringComparer.Ordinal);
    foreach (var frame in replay.Frames)
    {
        var suggested = profiles.Select(profile => PersonaShadowPolicy.Propose(frame, profile, keys, seed)).ToArray();
        foreach (var proposal in suggested)
            if (proposal.ProposedAction is not null)
                proposals[proposal.PersonaId] = proposals.GetValueOrDefault(proposal.PersonaId) + 1;

        await JsonSerializer.SerializeAsync(output, new
        {
            frame.Step,
            frame.Phase,
            frame.Action,
            candidates = suggested,
            note = "SHADOW ONLY: these proposals were never sent to the game; no actual game performance is inferred."
        }, options);
        await output.WriteAsync(new byte[] { (byte)'\n' });
    }

    var contrast = PersonaContrastAnalyzer.Analyze(Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)),
        replay, profiles, keys, seed);
    var reportOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
    await File.WriteAllTextAsync(Path.Combine(folder, "persona-contrast.json"),
        JsonSerializer.Serialize(contrast, reportOptions));
    await File.WriteAllTextAsync(Path.Combine(folder, "persona-contrast.md"),
        PersonaContrastMarkdown.Render(contrast));

    var ablations = PersonaTraitAblations.Analyze(replay, profiles[1], keys, seed);
    await File.WriteAllTextAsync(Path.Combine(folder, "persona-ablation.json"),
        JsonSerializer.Serialize(ablations, reportOptions));

    Console.WriteLine($"ARGUS shadow personas analyzed {replay.Frames.Count} previously saved visual frames.");
    foreach (var profile in profiles)
        Console.WriteLine($"{profile.Id}: {proposals.GetValueOrDefault(profile.Id)} hypothetical actions (not executed).");
    foreach (var pair in contrast.Pairs)
        Console.WriteLine($"{pair.FirstPersona} vs {pair.SecondPersona}: " +
            $"{pair.DifferentDirections}/{pair.BothMovingFrames} different movement directions, " +
            $"{pair.DifferentFullActions}/{pair.BothProposedFrames} any action differences.");
    Console.WriteLine($"Saved local shadow proposals, contrasts and one-trait ablations alongside {path}.");
    Console.WriteLine("Proposed actions were never executed; no game outcome or human realism was inferred.");
    return 0;
}
catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine("ARGUS shadow personas failed: " + ex.Message);
    return 1;
}
