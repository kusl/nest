namespace Nest.Web.Listings;

internal sealed record WindowChange(Entry[] Next, Entry[] Added, Guid[] Removed);

internal static class EntryWindow
{
    public static WindowChange? Merge(Entry[] current, IEnumerable<Entry> candidates, int capacity)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candidates);
        var known = new HashSet<Guid>(current.Length);
        foreach (var entry in current)
        {
            known.Add(entry.Id);
        }

        var next = current
            .Concat(candidates.Where(entry => !known.Contains(entry.Id)).DistinctBy(entry => entry.Id))
            .OrderByDescending(entry => entry.Id)
            .Take(capacity)
            .ToArray();
        var added = next.Where(entry => !known.Contains(entry.Id)).ToArray();
        if (added.Length == 0)
        {
            return null;
        }

        var kept = next.Select(entry => entry.Id).ToHashSet();
        var removed = current.Where(entry => !kept.Contains(entry.Id)).Select(entry => entry.Id).ToArray();
        return new WindowChange(next, added, removed);
    }

    public static bool IsRestorable(Guid id, DateTimeOffset now, DateTimeOffset startedAt)
    {
        if (!Uuid7.IsValid(id))
        {
            return false;
        }

        var at = Uuid7.Timestamp(id);
        return at <= now && at < startedAt;
    }
}
