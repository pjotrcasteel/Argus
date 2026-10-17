using Argus.Core;
using Microsoft.Playwright;

namespace Argus.Adapters.Browser;

/// <summary>Black-box interaction through screenshot pixels, mouse and keyboard only.</summary>
public sealed class PlaywrightGameEnvironment(bool headless, int width, int height) : IVisualGameEnvironment
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    public async Task OpenAsync(Uri address, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_page is not null) throw new InvalidOperationException("This environment is already open.");

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = headless });
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = width, Height = height },
            DeviceScaleFactor = 1
        });
        _page = await _context.NewPageAsync();
        await _page.GotoAsync(address.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
    }

    public async Task<FrameObservation> ObserveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = _page ?? throw new InvalidOperationException("Open the environment before observing.");
        var screenshot = await page.ScreenshotAsync(new PageScreenshotOptions { Type = ScreenshotType.Png });
        return new FrameObservation(DateTimeOffset.UtcNow, width, height, screenshot);
    }

    public async Task ExecuteAsync(GameAction action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = _page ?? throw new InvalidOperationException("Open the environment before sending actions.");
        switch (action.Kind)
        {
            case GameActionKind.Wait:
                await Task.Delay(Math.Clamp(action.DurationMs, 0, 60_000), cancellationToken);
                break;
            case GameActionKind.MovePointer:
                await page.Mouse.MoveAsync(action.X, action.Y);
                break;
            case GameActionKind.Click:
                await page.Mouse.ClickAsync(action.X, action.Y);
                break;
            case GameActionKind.KeyDown:
                await page.Keyboard.DownAsync(RequiredKey(action));
                break;
            case GameActionKind.KeyUp:
                await page.Keyboard.UpAsync(RequiredKey(action));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "Unsupported player action.");
        }
    }

    private static string RequiredKey(GameAction action) =>
        !string.IsNullOrWhiteSpace(action.Key) ? action.Key : throw new ArgumentException("Key action requires Key.", nameof(action));

    public async ValueTask DisposeAsync()
    {
        if (_context is not null) await _context.CloseAsync();
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }
}
