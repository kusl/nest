namespace Nest.Web.Listings;

internal sealed record ListingSource(string Key, string Provider, string Url, Guid Id);

internal sealed record Listing(
    string Key,
    IReadOnlyList<string> Keys,
    IReadOnlyList<ListingSource> Links,
    IReadOnlyDictionary<string, string> Fields,
    IReadOnlyList<Entry> Comments,
    IReadOnlyList<Guid> Ids,
    Guid Updated,
    string Title)
{
    public string Place => ListingFields.Place(Fields);

    public string Summary => ListingFields.Summary(Fields);

    public string Sources => string.Join(" · ", Links.Select(link => ListingKey.ProviderLabel(link.Provider)).Distinct(StringComparer.Ordinal));
}
