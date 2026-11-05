using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Nest.E2E;

internal static class Pages
{
    public const string StoredTexts = """
        () => new Promise(resolve => {
            const request = indexedDB.open('nest');
            request.onupgradeneeded = () => request.transaction.abort();
            request.onsuccess = () => {
                const db = request.result;
                const all = db.transaction('entries', 'readonly').objectStore('entries').getAll();
                all.onsuccess = () => { db.close(); resolve(all.result.filter(e => e.text).map(e => e.text)); };
                all.onerror = () => { db.close(); resolve([]); };
            };
            request.onerror = () => resolve([]);
        })
        """;

    public const string Seed = """
        records => new Promise((resolve, reject) => {
            const request = indexedDB.open('nest', 1);
            request.onupgradeneeded = () => request.result.createObjectStore('entries', { keyPath: 'id' });
            request.onsuccess = () => {
                const db = request.result;
                const transaction = db.transaction('entries', 'readwrite');
                for (const record of records) { transaction.objectStore('entries').put(record); }
                transaction.oncomplete = () => { db.close(); resolve(); };
                transaction.onerror = () => reject(transaction.error);
            };
            request.onerror = () => reject(request.error);
        })
        """;

    public const string IdAt = """
        ms => {
            const hex = ms.toString(16).padStart(12, '0');
            const random = Array.from(crypto.getRandomValues(new Uint8Array(10)), b => b.toString(16).padStart(2, '0')).join('');
            return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-7${random.slice(0, 3)}-8${random.slice(3, 6)}-${random.slice(6, 18)}`;
        }
        """;

    public static int UniqueNumber() => Random.Shared.Next(10_000_000, 99_999_999);

    public static async Task<IPage> OpenHomesAsync(IBrowserContext context)
    {
        var page = await context.NewPageAsync();
        await page.GotoAsync("/");
        await WaitLiveAsync(page);
        return page;
    }

    public static async Task WaitLiveAsync(IPage page)
    {
        await Expect(page.Locator("#sync-status")).ToHaveAttributeAsync("data-status", "live");
    }

    public static async Task<IPage> AddLinkAsync(IBrowserContext context, string text)
    {
        var page = await OpenHomesAsync(context);
        await page.Locator("#link-input").FillAsync(text);
        await page.GetByRole(AriaRole.Button, new() { Name = "Open" }).ClickAsync();
        await Expect(page.Locator("#listing-title")).ToBeVisibleAsync();
        await WaitLiveAsync(page);
        return page;
    }

    public static async Task SaveDetailsAsync(IPage page, params (string Key, string Value)[] values)
    {
        var editor = page.Locator("#facts-editor");
        if (await editor.GetAttributeAsync("open") is null)
        {
            await editor.Locator("summary").ClickAsync();
        }

        foreach (var (key, value) in values)
        {
            var field = page.Locator($"#fact-{key}");
            if (await field.EvaluateAsync<string>("element => element.tagName") == "SELECT")
            {
                await field.SelectOptionAsync(value);
            }
            else
            {
                await field.FillAsync(value);
            }
        }

        await page.GetByRole(AriaRole.Button, new() { Name = "Save details" }).ClickAsync();
        await Expect(page.Locator("#facts-status")).ToHaveTextAsync("Saved.");
    }

    public static async Task CommentAsync(IPage page, string text)
    {
        await page.Locator("#comment-text").FillAsync(text);
        await page.Locator("#comment-text").PressAsync("Control+Enter");
        await Expect(page.Locator("#comments .comment", new() { HasText = text })).ToBeVisibleAsync();
    }

    public static async Task EventuallyAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.True(await condition());
    }
}
