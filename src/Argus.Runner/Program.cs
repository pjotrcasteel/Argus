using System.Text.Json;
using System.Text.Json.Serialization;
using Argus.Adapters.Browser;
using Argus.Agents.Visual;
using Argus.Core;

var options = CommandOptions.Parse(args);
if (options is null)
{
    Console.Error.WriteLine("Usage: dotnet run --project src/Argus.Runner -- --url https://example.org [--agent observe|script|visual] [--profile examples/wyrmforge.profile.json] [--script examples/actions.example.json] [--headless] [--require-combat-vision] [--max-steps 80] [--timeout-seconds 180] [--output results/demo]");
    return 2;
}

var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
Directory.CreateDirectory(options.Output);

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    IVisualAgent agent = options.Agent switch
    {
        "observe" => new ScriptedAgent([]),
        "script" => new ScriptedAgent(JsonSerializer.Deserialize<GameAction[]>(await File.ReadAllTextAsync(options.Script!, cts.Token), jsonOptions) ?? []),
        "visual" => CreateVisualAgent(options, jsonOptions),
        _ => throw new ArgumentException("Unsupported agent mode: " + options.Agent)
    };

    await using IVisualGameEnvironment environment = new PlaywrightGameEnvironment(options.Headless, options.Width, options.Height);
    await environment.OpenAsync(new Uri(options.Url), cts.Token);
    await using var trace = new FileStream(Path.Combine(options.Output, "trace.jsonl"), FileMode.Create, FileAccess.Write);
    var evidenceCollector = new RunEvidenceCollector();

    for (var step = 0; step <= options.MaxSteps; step++)
    {
        cts.Token.ThrowIfCancellationRequested();
        var frame = await environment.ObserveAsync(cts.Token);
        var filename = $"frame-{step:D4}.png";
        await File.WriteAllBytesAsync(Path.Combine(options.Output, filename), frame.PngBytes, cts.Token);
        // Every saved PNG must be assessed against its own pixels. Previously the final step
        // reused LastEvidence from the PREVIOUS screenshot, corrupting blind frame scoring.
        // Make a final observation but suppress its proposed input at the step limit.
        var proposedAction = await agent.DecideAsync(frame, cts.Token);
        var inputSuppressedAtStepLimit = step == options.MaxSteps;
        var action = inputSuppressedAtStepLimit ? null : proposedAction;
        var evidence = (agent as IAgentEvidenceProvider)?.LastEvidence;
        evidenceCollector.Record(action, evidence);

        await JsonSerializer.SerializeAsync(trace, new
        {
            step, frame.TimestampUtc, frame.Width, frame.Height, screenshot = filename, action, evidence, inputSuppressedAtStepLimit
        }, jsonOptions, cts.Token);
        await trace.WriteAsync(new byte[] { (byte)'\n' }, cts.Token);
        Console.WriteLine($"[{step}] {evidence?.Phase ?? options.Agent}: {evidence?.Reason ?? "screenshot"}; action: {action?.Kind.ToString() ?? "stop"}");

        if (action is null) break;
        await environment.ExecuteAsync(action, cts.Token);
    }

    var summary = evidenceCollector.Snapshot();
    await File.WriteAllTextAsync(Path.Combine(options.Output, "summary.json"),
        JsonSerializer.Serialize(summary, jsonOptions), cts.Token);
    Console.WriteLine("Saved local screenshots, trace and evidence summary: " + Path.GetFullPath(options.Output));
    Console.WriteLine($"Evidence gate: {(summary.MeetsVisualCombatGate ? "PASS" : "NOT MET")} — " +
        $"{summary.PlayerLocatedFrames} visual player frames, {summary.RouteClicks} route clicks, {summary.TerminalStops} terminal stops.");
    if (options.RequireCombatVision && !summary.MeetsVisualCombatGate)
    {
        Console.Error.WriteLine("ARGUS M1 visual-combat evidence gate not met: see summary.json and screenshots.");
        return 1;
    }
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("ARGUS stopped by deadline or user. Partial local evidence may be available.");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine("ARGUS runner failed: " + ex);
    return 1;
}

static IVisualAgent CreateVisualAgent(CommandOptions options, JsonSerializerOptions jsonOptions)
{
    var json = File.ReadAllText(options.Profile!);
    var profile = JsonSerializer.Deserialize<VisualGameProfile>(json, jsonOptions)
        ?? throw new InvalidDataException("Invalid visual game profile.");
    profile.Validate();
    return new VisualGameAgent(new TesseractTextReader(), profile);
}

internal sealed record CommandOptions(string Url, string Agent, string? Script, string? Profile, string Output, bool Headless, int Width,
    int Height, int MaxSteps, int TimeoutSeconds, bool RequireCombatVision)
{
    public static CommandOptions? Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var headless = false;
        var requireCombatVision = false;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--headless") { headless = true; continue; }
            if (args[i] == "--require-combat-vision") { requireCombatVision = true; continue; }
            if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal)) return null;
            values[args[i]] = args[++i];
        }

        if (!values.TryGetValue("--url", out var url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https")) return null;

        var mode = values.GetValueOrDefault("--agent", "observe").ToLowerInvariant();
        if (mode is not ("observe" or "script" or "visual")) return null;
        var script = values.GetValueOrDefault("--script");
        var profile = values.GetValueOrDefault("--profile");
        if (mode == "script" && string.IsNullOrWhiteSpace(script)) return null;
        if (mode == "visual" && string.IsNullOrWhiteSpace(profile)) return null;

        if (!ParseInt(values, "--width", 390, 100, 3840, out var width) ||
            !ParseInt(values, "--height", 844, 100, 2160, out var height) ||
            !ParseInt(values, "--max-steps", 80, 1, 500, out var steps) ||
            !ParseInt(values, "--timeout-seconds", 180, 5, 1800, out var seconds)) return null;

        return new CommandOptions(url, mode, script, profile, values.GetValueOrDefault("--output", "results/first-observation"),
            headless, width, height, steps, seconds, requireCombatVision);
    }

    private static bool ParseInt(Dictionary<string, string> values, string key, int defaultValue, int minimum, int maximum, out int number)
    {
        number = defaultValue;
        return (!values.TryGetValue(key, out var text) || int.TryParse(text, out number)) && number >= minimum && number <= maximum;
    }
}
