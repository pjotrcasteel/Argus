using Argus.Replay;

if (args.Length != 2 || args[0] != "--run")
{
    Console.Error.WriteLine("Usage: dotnet run --project src/Argus.Replay.Tool -- --run results/your-run");
    return 2;
}

try
{
    var folder = Path.GetFullPath(args[1]);
    var trace = await ReplayTraceReader.ReadAsync(folder);
    var target = Path.Combine(folder, "replay.html");
    await File.WriteAllTextAsync(target, ReplayPageBuilder.Render(trace));
    Console.WriteLine($"ARGUS offline replay generated for {trace.Frames.Count} recorded observations: {target}");
    Console.WriteLine("Open replay.html in a browser. No server, login, internet or game integration is needed.");
    return 0;
}
catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
{
    Console.Error.WriteLine("ARGUS replay validation failed: " + ex.Message);
    return 1;
}
