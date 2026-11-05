using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Nest.E2E;

public sealed partial class SiteTests(BrowserFixture browser) : IClassFixture<BrowserFixture>
{
    [Fact]
    public async Task PagesLoadWithoutErrorsOrCookies()
    {
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                errors.Add(message.Text);
            }
        };
        page.PageError += (_, error) => errors.Add(error);

        foreach (var path in new[] { "/about", "/homes/zillow/123", "/" })
        {
            await page.GotoAsync(path);
        }

        await Pages.WaitLiveAsync(page);
        Assert.Empty(errors);
        Assert.Empty(await context.CookiesAsync());
    }

    [Fact]
    public async Task OnlyTheHomePagesOpenAWebSocket()
    {
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var sockets = new List<string>();
        page.WebSocket += (_, socket) => sockets.Add(socket.Url);

        await page.GotoAsync("/about");
        await page.WaitForTimeoutAsync(1000);
        Assert.Empty(sockets);

        await page.GotoAsync("/");
        await Pages.WaitLiveAsync(page);
        Assert.NotEmpty(sockets);
    }

    [Theory]
    [InlineData(320, 640)]
    [InlineData(390, 844)]
    public async Task NoPageScrollsSidewaysOnAPhone(int width, int height)
    {
        await using var context = await browser.NewContextAsync(width, height);
        var number = Pages.UniqueNumber();
        var page = await Pages.AddLinkAsync(context, $"https://www.zillow.com/homedetails/{new string('x', 120)}/{number}_zpid/");
        await Pages.CommentAsync(page, $"{Guid.NewGuid():N}{new string('w', 300)}");

        foreach (var path in new[] { "/", $"/homes/zillow/{number}", "/about", "/no-such-page" })
        {
            await page.GotoAsync(path);
            var nav = page.GetByRole(AriaRole.Navigation, new() { Name = "Main" });
            await Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Homes", Exact = true })).ToBeInViewportAsync();
            var widths = await page.EvaluateAsync<int[]>("() => [document.documentElement.scrollWidth, document.documentElement.clientWidth]");
            Assert.True(widths[0] <= widths[1], $"{path} is {widths[0]} px wide in a {widths[1]} px viewport.");
        }
    }

    [Fact]
    public async Task ThemeChoiceSurvivesReloadAndNavigation()
    {
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var root = page.Locator("html");

        await page.GotoAsync("/about");
        await page.GetByRole(AriaRole.Button, new() { Name = "Dark theme" }).ClickAsync();
        await Expect(root).ToHaveAttributeAsync("data-theme", "dark");

        await page.ReloadAsync();
        await Expect(root).ToHaveAttributeAsync("data-theme", "dark");

        await page.GetByRole(AriaRole.Navigation).GetByRole(AriaRole.Link, new() { Name = "Homes" }).ClickAsync();
        await Expect(page).ToHaveURLAsync(HomeUrl());
        await Expect(root).ToHaveAttributeAsync("data-theme", "dark");
    }

    [Fact]
    public async Task ResponsesCarryAStrictPolicyAndACustomNotFoundPage()
    {
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        var home = await page.GotoAsync("/") ?? throw new InvalidOperationException("No response.");
        Assert.StartsWith("default-src 'self'; script-src 'self'", home.Headers["content-security-policy"], StringComparison.Ordinal);

        var missing = await page.GotoAsync("/no-such-page") ?? throw new InvalidOperationException("No response.");
        Assert.Equal(404, missing.Status);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Nothing here" })).ToBeVisibleAsync();
    }

    [GeneratedRegex("/$")]
    private static partial Regex HomeUrl();
}
