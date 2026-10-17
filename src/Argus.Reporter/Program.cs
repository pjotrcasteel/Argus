using System.Text.Json;
using Argus.Analysis;

if (args.Length != 4 || args[0] != "--runs" || args[2] != "--output")
{
    Console.Error.WriteLine("Usage: dotnet run --project src/Argus.Reporter -- --runs results/experiments --output results/cohort");
    return 2;
}

try
{
    var report = await CohortReader.ReadAsync(args[1]);
    var destination = Path.GetFullPath(args[3]);
    Directory.CreateDirectory(destination);
    var json = JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    await File.WriteAllTextAsync(Path.Combine(destination, "cohort.json"), json);
    await File.WriteAllTextAsync(Path.Combine(destination, "cohort.md"), CohortMarkdown.Render(report));
    Console.WriteLine($"Analyzed {report.Totals.Runs} independent runs; gate passed {report.Totals.PassedVisualGate}.");
    Console.WriteLine("Comparison status: " + (report.Totals.IsMatchedCohort ? "MATCHED" : "DESCRIPTIVE ONLY"));
    Console.WriteLine("Saved cohort.json and cohort.md to " + destination);
    return 0;
}
catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or JsonException)
{
    Console.Error.WriteLine("ARGUS cohort report failed: " + ex.Message);
    return 1;
}
