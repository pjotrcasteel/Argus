using System.Text.Json;
using System.Text.Json.Serialization;
using Argus.Adapters.Browser;
using Argus.Core;

var options = CommandOptions.Parse(args);
if (options is null)
{
    Console.Error.WriteLine("Usage: dotnet run --project src/Argus.Runner -- --url https://example.org [--headless] [--script examples/actions.example.json] [--output results/demo] [--width 390] [--height 844]");
    return 2;
}

Directory.CreateDirectory(options.Output);
var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
GameAction[] actions = options.Script is null
    ? []
    : JsonSerializer.Deserialize<GameAction[]>(await File.ReadAllTextAsync(options.Script), jsonOptions) ?? [];

await using IVisualGameEnvironment environment = new PlaywrightGameEnvironment(options.Headless, options.Width, options.Height);
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
try
{
    await environment.OpenAsync(new Uri(options.Url), cts.Token);
    IVisualAgent agent = new ScriptedAgent(actions);
    await using var trace = new FileStream(Path.Combine(options.Output, "trace.jsonl"), FileMode.Create, FileAccess.Write);
    var step = 0;

    while (step <= actions.Length)
    {
        var frame = await environment.ObserveAsync(cts.Token);
        var imageFile = $"frame-{step:D4}.png";
        await File.WriteAllBytesAsync(Path.Combine(options.Output, imageFile), frame.PngBytes, cts.Token);
        var action = await agent.DecideAsync(frame, cts.Token);
        var record = new { step, frame.TimestampUtc, frame.Width, frame.Height, screenshot = imageFile, action };
        await JsonSerializer.SerializeAsync(trace, record, jsonOptions, cts.Token);
        await trace.WriteAsync(new byte[] { (byte)'\n' }, cts.Token);
        Console.WriteLine($"Captured {imageFile}" + (action is null ? " (done)" : $"; action: {action.Kind}"));
        if (action is null) break;
        await environment.ExecuteAsync(action, cts.Token);
        step++;
    }

    Console.WriteLine($"Evidence written to: {Path.GetFullPath(options.Output)}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ARGUS runner failed: {ex.Message}");
    return 1;
}

internal sealed record CommandOptions(string Url, string Output, string? Script, bool Headless, int Width, int Height)
{
    public static CommandOptions? Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var headless = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--headless") { headless = true; continue; }
            if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal)) return null;
            values[args[i]] = args[++i];
        }

        if (!values.TryGetValue("--url", out var url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) return null;
        if (!int.TryParse(values.GetValueOrDefault("--width", "390"), out var width) || width is < 100 or > 3840) return null;
        if (!int.TryParse(values.GetValueOrDefault("--height", "844"), out var height) || height is < 100 or > 2160) return null;
        return new CommandOptions(url, values.GetValueOrDefault("--output", "results/first-observation"), values.GetValueOrDefault("--script"), headless, width, height);
    }
}
