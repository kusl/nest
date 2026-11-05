using System.Text.Json;
using Microsoft.Extensions.Options;
using Nest.Web.Infrastructure;
using Nest.Web.Listings;

namespace Nest.Web.Tests;

internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    private long _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow() => _now;

    public override long GetTimestamp() => _timestamp;

    public void Advance(TimeSpan by)
    {
        _now += by;
        _timestamp += by.Ticks;
    }
}

internal static class Limits
{
    public static IOptions<NestOptions> Create(Action<NestOptions>? configure = null)
    {
        var options = new NestOptions();
        configure?.Invoke(options);
        return Options.Create(options);
    }
}

internal sealed class NestRecorder(bool accept = true) : INestSubscriber
{
    public List<NestUpdate> Updates { get; } = [];

    public bool Deliver(NestUpdate update)
    {
        if (accept)
        {
            Updates.Add(update);
        }

        return accept;
    }
}

internal sealed record LinkVector(string Input, string? Key, string? Url);

internal sealed record AddressVector(string Street, string Unit, string Zip, string? Key);

internal sealed record FieldVector(string Key, string Input, bool Ok, string? Value);

internal sealed record ListingVector(string Key, string[] Keys, string[] Links, Dictionary<string, string> Fields, string[] Comments, string Updated, string Title);

internal sealed record ListingScenario(EntryWire[] Entries, ListingVector[] Listings);

internal static class Fixtures
{
    public static T Load<T>(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", name + ".json");
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Wire.Options)
            ?? throw new InvalidOperationException($"The fixture {name} is empty.");
    }

    public static ListingScenario Listings() => Load<ListingScenario>("listings");

    public static Entry[] ListingEntries() =>
        [.. Listings().Entries.Select(wire => wire.ToEntry() ?? throw new InvalidOperationException("Bad fixture entry."))];
}

internal static class Drafts
{
    public static EntryDraft Link(string url, params string[] anchors) => new(EntryKinds.Link, anchors, url, null, null);

    public static EntryDraft Comment(string text, params string[] anchors) => new(EntryKinds.Comment, anchors, null, null, text);

    public static EntryDraft Fact(string[] anchors, params (string Key, string Value)[] fields) =>
        new(EntryKinds.Fact, anchors, null, [.. fields.Select(field => new FieldWire(field.Key, field.Value))], null);
}
