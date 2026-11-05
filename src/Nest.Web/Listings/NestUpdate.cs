namespace Nest.Web.Listings;

internal sealed record NestUpdate(string Kind, long Version, long Previous, int Capacity, IReadOnlyList<Guid> Ids, IReadOnlyList<Entry> Added, IReadOnlyList<Guid> Removed)
{
    public static NestUpdate Snapshot(long version, int capacity, IReadOnlyList<Entry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return new("snapshot", version, version, capacity, entries.Select(entry => entry.Id).ToArray(), [], []);
    }

    public static NestUpdate Delta(long version, long previous, IReadOnlyList<Entry> added, IReadOnlyList<Guid> removed) =>
        new("delta", version, previous, 0, [], added, removed);
}
