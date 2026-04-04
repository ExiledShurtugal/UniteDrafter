using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace UniteDrafter.SourceUpdate.Data.Updating;

internal sealed class SourceUpdateBrowserSession : IAsyncDisposable
{
    private readonly IPlaywright playwright;

    private SourceUpdateBrowserSession(
        IPlaywright playwright,
        IBrowserContext context,
        IPage page,
        ConcurrentQueue<BrowserResponseCapture> recentResponseBodies)
    {
        this.playwright = playwright;
        Context = context;
        Page = page;
        RecentResponseBodies = recentResponseBodies;
    }

    public IBrowserContext Context { get; }
    public IPage Page { get; }
    public ConcurrentQueue<BrowserResponseCapture> RecentResponseBodies { get; }

    public static async Task<SourceUpdateBrowserSession> CreateAsync(SourceUpdateOptions options)
    {
        Directory.CreateDirectory(options.BrowserProfileDirectory);

        var playwright = await Playwright.CreateAsync();
        var context = await playwright.Chromium.LaunchPersistentContextAsync(
            options.BrowserProfileDirectory,
            new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = options.Headless,
                ExecutablePath = ResolveEdgePath(),
                ViewportSize = null,
                Args =
                [
                    "--disable-blink-features=AutomationControlled"
                ]
            });

        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
        page.SetDefaultNavigationTimeout(60000);
        page.SetDefaultTimeout(60000);
        var recentResponseBodies = SourceUpdateBrowserPayloadExtractor.AttachResponseCapture(page);

        return new SourceUpdateBrowserSession(playwright, context, page, recentResponseBodies);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        playwright.Dispose();
    }

    private static string ResolveEdgePath()
    {
        var candidates = new[]
        {
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe"),
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Microsoft\Edge\Application\msedge.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Could not find Microsoft Edge. Install Edge or adjust the updater to use another Chromium browser.");
    }
}
