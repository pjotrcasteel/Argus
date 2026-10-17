using System.Text.Json;
using Argus.Evaluation;
using Argus.Replay;
using Microsoft.Playwright;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project tests/Argus.Annotation.UiSmoke -- results/my-run");
    return 2;
}

var folder = Path.GetFullPath(args[0]);
var pagePath = Path.Combine(folder, "annotate.html");
var tempFile = Path.Combine(Path.GetTempPath(), "argus-ui-smoke-" + Guid.NewGuid().ToString("N") + ".json");

try
{
    if (!File.Exists(pagePath)) throw new FileNotFoundException("Generate annotate.html before running UI smoke.", pagePath);
    var replay = await ReplayTraceReader.ReadAsync(folder);

    using var playwright = await Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    var context = await browser.NewContextAsync(new BrowserNewContextOptions { AcceptDownloads = true });
    var page = await context.NewPageAsync();
    await page.GotoAsync(new Uri(pagePath).AbsoluteUri, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

    var image = page.Locator("#frame");
    await image.WaitForAsync();
    var loaded = await image.EvaluateAsync<bool>("img => img.complete && img.naturalWidth > 0");
    if (!loaded) throw new InvalidDataException("Real ARGUS screenshot was not displayed in annotator.");
    var rectangle = await image.BoundingBoxAsync() ?? throw new InvalidDataException("Screenshot is not visible.");

    await page.Locator("#threat-mode").ClickAsync();
    await page.Mouse.ClickAsync(rectangle.X + rectangle.Width * 0.25f, rectangle.Y + rectangle.Height * 0.40f);
    var threatStatus = await page.Locator("#threat-status").TextContentAsync() ?? "";
    if (!threatStatus.Contains("known (1 point(s))", StringComparison.Ordinal))
        throw new InvalidDataException("Clicking the image did not register a manual threat point.");

    await page.Locator("#player-mode").ClickAsync();
    await page.Mouse.ClickAsync(rectangle.X + rectangle.Width * 0.5f, rectangle.Y + rectangle.Height * 0.5f);
    var playerStatus = await page.Locator("#player-status").TextContentAsync() ?? "";
    if (!playerStatus.Contains("visible", StringComparison.Ordinal))
        throw new InvalidDataException("Clicking the image did not register the manual player center.");

    var downloadTask = page.WaitForDownloadAsync();
    await page.Locator("#export").ClickAsync();
    var download = await downloadTask;
    if (download.SuggestedFilename != "labels.json") throw new InvalidDataException("Unexpected annotation export filename.");
    await download.SaveAsAsync(tempFile);

    var labels = JsonSerializer.Deserialize<LabelSet>(await File.ReadAllTextAsync(tempFile),
        new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidDataException("Invalid exported JSON.");
    GroundTruth.Validate(labels, Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)), replay);
    if (labels.Frames[0].Player.State != "visible" || labels.Frames[0].Threats.Centers?.Count != 1)
        throw new InvalidDataException("Downloaded JSON did not preserve the two human UI actions.");

    // All browser-smoke marks are synthetic clicks for UI verification and MUST NOT be kept as real labels.
    await context.CloseAsync();
    Console.WriteLine("ARGUS offline annotator UI PASS: screenshot loaded, clicks changed labels, exported schema validated.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("ARGUS annotation browser-smoke FAILED: " + ex);
    return 1;
}
finally
{
    if (File.Exists(tempFile)) File.Delete(tempFile);
}
