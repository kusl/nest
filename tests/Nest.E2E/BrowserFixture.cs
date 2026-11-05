using Microsoft.Playwright;
using Xunit.Sdk;
using Xunit.v3;

[assembly: Parallelization(Mode = ParallelMode.None)]

namespace Nest.E2E;

public sealed class BrowserFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public static string BaseUrl { get; } = Environment.GetEnvironmentVariable("NEST_BASE_URL") ?? "http://127.0.0.1:5080/";

    public async ValueTask InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }

    public Task<IBrowserContext> NewContextAsync(int width = 1280, int height = 800)
    {
        var browser = _browser ?? throw new InvalidOperationException("The browser is not running.");
        return browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            ColorScheme = ColorScheme.Light,
            ViewportSize = new ViewportSize { Width = width, Height = height },
        });
    }
}
