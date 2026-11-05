using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nest.Web.Infrastructure;
using Nest.Web.Listings;

namespace Nest.Web.Tests;

public sealed class NestFactory : WebApplicationFactory<Program>
{
    public const string AllowedOrigin = "https://nest.example";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSetting("Nest:AllowedOrigins:0", AllowedOrigin);
        builder.UseSetting("Nest:PostBurst", "1000");
    }
}

public sealed class HttpPipelineTests(NestFactory factory) : IClassFixture<NestFactory>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/", "Nest")]
    [InlineData("/about", "About Nest")]
    [InlineData("/homes/zillow/123", "Zillow 123")]
    public async Task PagesRenderWithoutSettingCookies(string path, string title)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(path, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Contains($"<title>{title}", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HomePageRendersServerListings()
    {
        var nest = factory.Services.GetRequiredService<NestService>();
        var id = Random.Shared.Next(1_000_000, 9_999_999);
        var reply = nest.Submit(Drafts.Link($"https://www.redfin.com/home/{id}"), "integration");
        Assert.Equal(SubmitResult.Accepted, reply.Result);
        nest.Submit(Drafts.Fact([$"redfin:{id}"], ("street", $"{id} Integration Way"), ("zip", "13777"), ("beds", "3")), "integration");

        using var client = CreateClient();
        var html = await client.GetStringAsync("/", Cancellation);
        Assert.Contains($"href=\"homes/redfin/{id}\"", html, StringComparison.Ordinal);
        Assert.Contains($"{id} Integration Way", html, StringComparison.Ordinal);
        Assert.Contains("3 bd", html, StringComparison.Ordinal);
        Assert.Contains("data-view=\"homes\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListingPageRendersMergedDetailsAndComments()
    {
        var nest = factory.Services.GetRequiredService<NestService>();
        var id = Random.Shared.Next(1_000_000, 9_999_999);
        nest.Submit(Drafts.Link($"https://www.zillow.com/homedetails/{id}_zpid/"), "integration");
        nest.Submit(Drafts.Fact([$"zillow:{id}"], ("street", $"{id} Merge Road"), ("zip", "10019"), ("price", "450k")), "integration");
        nest.Submit(Drafts.Comment("Lovely porch 🏡", $"address:10019-{id}-merge-rd"), "integration");

        using var client = CreateClient();
        var html = await client.GetStringAsync($"/homes/address/10019-{id}-merge-rd", Cancellation);
        Assert.Contains("<h1 id=\"listing-title\"", html, StringComparison.Ordinal);
        Assert.Contains($"{id} Merge Road", html, StringComparison.Ordinal);
        Assert.Contains("$450,000", html, StringComparison.Ordinal);
        Assert.Contains("Open on Zillow", html, StringComparison.Ordinal);
        Assert.Contains("Lovely porch", html, StringComparison.Ordinal);
        Assert.Contains($"data-key=\"address:10019-{id}-merge-rd\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"facts-form\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownHomesStillRenderSoTheBrowserCanLook()
    {
        using var client = CreateClient();
        var html = await client.GetStringAsync("/homes/redfin/42", Cancellation);
        Assert.Contains("id=\"listing-missing\"", html, StringComparison.Ordinal);
        Assert.Contains("data-key=\"redfin:42\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/homes/zillow/abc")]
    [InlineData("/homes/mars/1")]
    [InlineData("/homes/address/1-main")]
    public async Task InvalidHomeAddressesAreNotFound(string path)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(path, Cancellation);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Nothing here", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResponsesCarrySecurityHeaders()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/", Cancellation);
        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.StartsWith("default-src 'self'; script-src 'self'", SecurityHeaders.ContentSecurityPolicy, StringComparison.Ordinal);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal(SecurityHeaders.PermissionsPolicy, response.Headers.GetValues("Permissions-Policy").Single());
        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public async Task HealthEndpointReportsHealthy()
    {
        using var client = CreateClient();
        Assert.Equal("Healthy", await client.GetStringAsync("/health", Cancellation));
    }

    [Fact]
    public async Task UnknownPathsGetTheNotFoundPage()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/no-such-page", Cancellation);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Nothing here", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://evil.example", HttpStatusCode.Forbidden)]
    [InlineData(NestFactory.AllowedOrigin, HttpStatusCode.OK)]
    public async Task HubNegotiationChecksTheOrigin(string origin, HttpStatusCode expected)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/_blazor/negotiate?negotiateVersion=1");
        request.Headers.Add("Origin", origin);
        using var response = await client.SendAsync(request, Cancellation);
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task HubRefusesTransportsOtherThanWebSockets()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/_blazor?id=abc", Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PagesAreCompressedWhenAsked()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/about");
        request.Headers.AcceptEncoding.ParseAdd("br");
        using var response = await client.SendAsync(request, Cancellation);
        Assert.Contains("br", response.Content.Headers.ContentEncoding);
    }

    [Fact]
    public async Task FormPostsAreRejected()
    {
        using var client = CreateClient();
        using var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("link", "https://example.com")]);
        using var response = await client.PostAsync("/", content, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/app.css")]
    [InlineData("/theme-init.js")]
    [InlineData("/favicon.svg")]
    [InlineData("/js/keys.js")]
    [InlineData("/js/sync.js")]
    public async Task StaticAssetsAreServed(string path)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(path, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TheAboutPageLinksToTheSourceAndWarnsAboutSharing()
    {
        using var client = CreateClient();
        var html = await client.GetStringAsync("/about", Cancellation);
        Assert.Contains("https://github.com/kusl/nest", html, StringComparison.Ordinal);
        Assert.Contains("can't be edited, deleted or recalled", html, StringComparison.Ordinal);
        Assert.Contains("data-clear-local", html, StringComparison.Ordinal);
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
}
