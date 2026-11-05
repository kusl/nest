using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Nest.E2E;

public sealed partial class HomesTests(BrowserFixture browser) : IClassFixture<BrowserFixture>
{
    [Fact]
    public async Task APastedLinkOpensItsHomeForEveryone()
    {
        await using var first = await browser.NewContextAsync();
        await using var second = await browser.NewContextAsync();
        var watcher = await Pages.OpenHomesAsync(second);
        var number = Pages.UniqueNumber();

        var page = await Pages.AddLinkAsync(first, $"Look at this one https://www.zillow.com/homedetails/2893-State-Route-26-Glen-Aubrey-NY-13777/{number}_zpid/?utm_source=share.");

        await Expect(page).ToHaveURLAsync(new Regex($"/homes/zillow/{number}$"));
        await Expect(page.Locator("#listing-title")).ToHaveTextAsync($"Zillow {number}");
        await Expect(page.Locator("#listing-links a")).ToHaveAttributeAsync("href", $"https://www.zillow.com/homedetails/2893-State-Route-26-Glen-Aubrey-NY-13777/{number}_zpid/");
        await Expect(watcher.Locator($"#homes-list .home[data-key=\"zillow:{number}\"]")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task TheSameLinkWithOtherParametersOpensTheSameHome()
    {
        await using var context = await browser.NewContextAsync();
        var number = Pages.UniqueNumber();
        await Pages.AddLinkAsync(context, $"https://www.zillow.com/homedetails/{number}_zpid/");

        var page = await Pages.OpenHomesAsync(context);
        await page.Locator("#link-input").FillAsync($"https://zillow.com/homedetails/Some-Street-City-ST-12345/{number}_zpid/?mmlb=g,0");
        await Expect(page.Locator("#link-preview")).ToContainTextAsync("Already here");
        await page.GetByRole(AriaRole.Button, new() { Name = "Open" }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex($"/homes/zillow/{number}$"));
        await Expect(page.Locator("#listing-links li")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task DetailsAreSharedLiveAndSuggestedFromTheLink()
    {
        await using var editorContext = await browser.NewContextAsync();
        await using var readerContext = await browser.NewContextAsync();
        var number = Pages.UniqueNumber();
        var editor = await Pages.AddLinkAsync(editorContext, $"https://www.realtor.com/realestateandhomes-detail/{number}-Oak-Ave_Glen-Aubrey_NY_13777_M{number}-11111");
        var reader = await readerContext.NewPageAsync();
        await reader.GotoAsync($"/homes/realtor/m{number}-11111");
        await Pages.WaitLiveAsync(reader);

        await editor.Locator("#facts-editor summary").ClickAsync();
        await Expect(editor.Locator("#facts-suggestion")).ToBeVisibleAsync();
        await Expect(editor.Locator("#fact-street")).ToHaveValueAsync($"{number} Oak Ave");
        await Expect(editor.Locator("#fact-zip")).ToHaveValueAsync("13777");

        await Pages.SaveDetailsAsync(editor, ("price", "450k"), ("beds", "3"), ("baths", "2.50"), ("status", "For sale"));

        await Expect(editor.Locator("#listing-title")).ToHaveTextAsync($"{number} Oak Ave");
        await Expect(reader.Locator("#listing-title")).ToHaveTextAsync($"{number} Oak Ave");
        await Expect(reader.Locator("#listing-summary")).ToHaveTextAsync("$450,000 · 3 bd · 2.5 ba · For sale");
        await Expect(reader.Locator("#listing-place")).ToHaveTextAsync("Glen Aubrey, NY 13777");

        await Pages.SaveDetailsAsync(reader, ("price", "435000"));
        await Expect(editor.Locator("#listing-facts")).ToContainTextAsync("$435,000 was $450,000");
    }

    [Fact]
    public async Task BadValuesAreExplainedBeforeSending()
    {
        await using var context = await browser.NewContextAsync();
        var page = await Pages.AddLinkAsync(context, $"https://www.redfin.com/home/{Pages.UniqueNumber()}");
        await page.Locator("#facts-editor summary").ClickAsync();
        await page.Locator("#fact-zip").FillAsync("123");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save details" }).ClickAsync();
        await Expect(page.Locator("#fact-zip-error")).ToHaveTextAsync("Use a five-digit ZIP code.");
        await Expect(page.Locator("#fact-zip")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task CommentsReachOtherBrowsersAndTheirStorage()
    {
        await using var writerContext = await browser.NewContextAsync();
        await using var readerContext = await browser.NewContextAsync();
        var number = Pages.UniqueNumber();
        var writer = await Pages.AddLinkAsync(writerContext, $"https://www.redfin.com/NY/Glen-Aubrey/1-Elm-St-13777/home/{number}");
        var reader = await readerContext.NewPageAsync();
        await reader.GotoAsync($"/homes/redfin/{number}");
        await Pages.WaitLiveAsync(reader);
        var text = $"Big yard 🌳 {Guid.NewGuid():N}";

        await Pages.CommentAsync(writer, text);

        await Expect(writer.Locator("#comments .comment.mine .yours")).ToHaveTextAsync("You");
        var item = reader.Locator("#comments .comment", new() { HasText = text });
        await Expect(item).ToBeVisibleAsync();
        await Expect(item).Not.ToHaveClassAsync(Archived());
        await Pages.EventuallyAsync(async () => (await reader.EvaluateAsync<string[]>(Pages.StoredTexts)).Contains(text));
        await Expect(writer.Locator("#comment-text")).ToHaveValueAsync("");
        await Expect(writer.Locator("#comment-count")).ToHaveTextAsync("0 / 4000");
    }

    [Fact]
    public async Task UnsentCommentsSurviveAReload()
    {
        await using var context = await browser.NewContextAsync();
        var page = await Pages.AddLinkAsync(context, $"https://www.redfin.com/home/{Pages.UniqueNumber()}");
        await page.Locator("#comment-text").PressSequentiallyAsync("half a thought");
        await page.ReloadAsync();
        await Expect(page.Locator("#comment-text")).ToHaveValueAsync("half a thought");
    }

    [Fact]
    public async Task HomesWithTheSameAddressMergeIntoOnePage()
    {
        await using var first = await browser.NewContextAsync();
        await using var second = await browser.NewContextAsync();
        var number = Pages.UniqueNumber();
        var zillow = await Pages.AddLinkAsync(first, $"https://www.zillow.com/homedetails/{number}_zpid/");
        await Pages.SaveDetailsAsync(zillow, ("street", $"{number} Maple Street"), ("zip", "13777"), ("beds", "3"));
        await Pages.CommentAsync(zillow, "Seen on Zillow");

        var redfin = await Pages.AddLinkAsync(second, $"https://www.redfin.com/home/{number}");
        await Pages.SaveDetailsAsync(redfin, ("street", $"{number} MAPLE ST"), ("zip", "13777"), ("baths", "2"));
        await Pages.CommentAsync(redfin, "Seen on Redfin");

        await Expect(zillow.Locator("#listing-links")).ToContainTextAsync("Open on Redfin");
        await Expect(zillow.Locator("#comments")).ToContainTextAsync("Seen on Redfin");
        await Expect(zillow.Locator("#listing-summary")).ToHaveTextAsync("3 bd · 2 ba");
        await Expect(redfin.Locator("#comments")).ToContainTextAsync("Seen on Zillow");

        var home = await Pages.OpenHomesAsync(first);
        await Expect(home.Locator("#homes-list .home", new() { HasText = $"{number} MAPLE ST" })).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task AHomeCanBeAddedByAddress()
    {
        await using var context = await browser.NewContextAsync();
        var page = await Pages.OpenHomesAsync(context);
        var number = Pages.UniqueNumber();
        await page.Locator("details.by-address summary").ClickAsync();
        await page.Locator("#add-street").FillAsync($"{number} Birch Lane Apt 4");
        await page.Locator("#add-zip").FillAsync("10019");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add home" }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex($"/homes/address/10019-{number}-birch-ln-unit-4$"));
        await Expect(page.Locator("#listing-title")).ToHaveTextAsync($"{number} Birch Lane Apt 4");
    }

    [Fact]
    public async Task BrowsersRestoreWhatTheServerForgot()
    {
        await using var keeper = await browser.NewContextAsync();
        await using var stranger = await browser.NewContextAsync();
        var number = Pages.UniqueNumber();
        var seeder = await keeper.NewPageAsync();
        await seeder.GotoAsync("/about");
        var old = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds();
        var linkId = await seeder.EvaluateAsync<string>(Pages.IdAt, old);
        var commentId = await seeder.EvaluateAsync<string>(Pages.IdAt, old + 1000);
        await seeder.EvaluateAsync(Pages.Seed, new object[]
        {
            new { id = linkId, kind = "link", keys = new[] { $"zillow:{number}" }, url = $"https://www.zillow.com/homedetails/{number}_zpid/" },
            new { id = commentId, kind = "comment", keys = new[] { $"zillow:{number}" }, text = "Kept by one browser" },
        });

        await seeder.GotoAsync($"/homes/zillow/{number}");
        await Expect(seeder.Locator("#comments .comment", new() { HasText = "Kept by one browser" })).Not.ToHaveClassAsync(Archived());

        var fresh = await stranger.NewPageAsync();
        await fresh.GotoAsync($"/homes/zillow/{number}");
        await Expect(fresh.Locator("#comments .comment", new() { HasText = "Kept by one browser" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task EntriesTheServerRefusedHaveADottedBorder()
    {
        await using var context = await browser.NewContextAsync();
        var number = Pages.UniqueNumber();
        var page = await context.NewPageAsync();
        await page.GotoAsync("/about");
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var id = await page.EvaluateAsync<string>(Pages.IdAt, now);
        await page.EvaluateAsync(Pages.Seed, new object[]
        {
            new { id, kind = "comment", keys = new[] { $"redfin:{number}" }, text = "Only in this browser" },
        });

        await page.GotoAsync($"/homes/redfin/{number}");
        await Pages.WaitLiveAsync(page);
        var item = page.Locator("#comments .comment", new() { HasText = "Only in this browser" });
        await Expect(item).ToHaveClassAsync(Archived());
        await Expect(item).ToHaveCSSAsync("border-top-style", "dotted");
    }

    [Fact]
    public async Task ClearingThisBrowserKeepsTheServerCopy()
    {
        await using var context = await browser.NewContextAsync();
        var number = Pages.UniqueNumber();
        var page = await Pages.AddLinkAsync(context, $"https://www.redfin.com/home/{number}");
        await Pages.CommentAsync(page, $"Remember me {number}");

        await page.GetByRole(AriaRole.Navigation).GetByRole(AriaRole.Link, new() { Name = "About" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete this browser's copy" }).ClickAsync();
        await Expect(page.Locator("#clear-status")).ToHaveTextAsync("This browser's copy is gone. Other browsers keep theirs.");
        Assert.Empty(await page.EvaluateAsync<string[]>(Pages.StoredTexts));

        await page.GotoAsync($"/homes/redfin/{number}");
        await Expect(page.Locator("#comments")).ToContainTextAsync($"Remember me {number}");
        await Pages.EventuallyAsync(async () => (await page.EvaluateAsync<string[]>(Pages.StoredTexts)).Contains($"Remember me {number}"));
    }

    [Fact]
    public async Task TheListCanBeFiltered()
    {
        await using var context = await browser.NewContextAsync();
        var number = Pages.UniqueNumber();
        var listing = await Pages.AddLinkAsync(context, $"https://www.redfin.com/home/{number}");
        await Pages.SaveDetailsAsync(listing, ("city", $"Filterville{number}"));
        var page = await Pages.OpenHomesAsync(context);

        await page.Locator("#homes-filter").FillAsync($"filterville{number}");
        await Expect(page.Locator("#homes-list .home")).ToHaveCountAsync(1);
        await page.Locator("#homes-filter").FillAsync($"nowhere{number}");
        await Expect(page.Locator("#homes-list .home")).ToHaveCountAsync(0);
        await Expect(page.Locator("#homes-empty")).ToHaveTextAsync("No homes match.");
    }

    [GeneratedRegex(@"\barchived\b")]
    private static partial Regex Archived();
}
