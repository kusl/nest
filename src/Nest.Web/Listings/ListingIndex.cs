namespace Nest.Web.Listings;

internal static class ListingIndex
{
    public static IReadOnlyList<Listing> Build(IEnumerable<Entry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        var usable = new List<Entry>();
        foreach (var entry in entries)
        {
            if (entry?.Keys is not { Count: > 0 } keys)
            {
                continue;
            }

            usable.Add(entry);
            for (var i = 1; i < keys.Count; i++)
            {
                Union(parents, keys[0], keys[i]);
            }
        }

        var groups = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        foreach (var entry in usable)
        {
            var root = Root(parents, entry.Keys[0]);
            if (!groups.TryGetValue(root, out var group))
            {
                group = [];
                groups[root] = group;
            }

            group.Add(entry);
        }

        var listings = groups.Values.Select(group => Resolve(group)).ToList();
        listings.Sort(static (a, b) =>
        {
            var updated = b.Updated.CompareTo(a.Updated);
            return updated != 0 ? updated : ListingKey.Compare(a.Key, b.Key);
        });
        return listings;
    }

    public static Listing? Find(IEnumerable<Listing> listings, string key)
    {
        ArgumentNullException.ThrowIfNull(listings);
        return listings.FirstOrDefault(listing => listing.Keys.Contains(key, StringComparer.Ordinal));
    }

    public static Listing Resolve(IReadOnlyList<Entry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var sorted = entries.OrderBy(entry => entry.Id).ToArray();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var links = new Dictionary<string, ListingSource>(StringComparer.Ordinal);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var comments = new List<Entry>();
        foreach (var entry in sorted)
        {
            keys.UnionWith(entry.Keys);
            switch (entry.Kind)
            {
                case EntryKinds.Link when ListingLink.Parse(entry.Url) is { } parsed:
                    links[parsed.Key] = new ListingSource(parsed.Key, parsed.Provider, entry.Url!, entry.Id);
                    break;
                case EntryKinds.Fact when entry.Fields is { } values:
                    foreach (var field in values)
                    {
                        fields[field.Key] = field.Value;
                    }

                    break;
                case EntryKinds.Comment:
                    comments.Add(entry);
                    break;
            }
        }

        foreach (var key in fields.Where(pair => pair.Value.Length == 0).Select(pair => pair.Key).ToList())
        {
            fields.Remove(key);
        }

        var orderedKeys = keys.Order(ListingKey.Comparer).ToArray();
        var primary = orderedKeys[0];
        var title = ListingFields.StreetLine(fields);
        if (title.Length == 0)
        {
            title = ListingFields.Place(fields);
        }

        if (title.Length == 0)
        {
            title = ListingKey.Label(primary);
        }

        return new Listing(
            primary,
            orderedKeys,
            links.Values.OrderBy(link => link.Key, ListingKey.Comparer).ToArray(),
            fields,
            comments,
            sorted.Select(entry => entry.Id).ToArray(),
            sorted[^1].Id,
            title);
    }

    private static string Root(Dictionary<string, string> parents, string key)
    {
        var root = key;
        while (parents.TryGetValue(root, out var parent) && !string.Equals(parent, root, StringComparison.Ordinal))
        {
            root = parent;
        }

        var node = key;
        while (!string.Equals(node, root, StringComparison.Ordinal))
        {
            var next = parents[node];
            parents[node] = root;
            node = next;
        }

        parents.TryAdd(root, root);
        return root;
    }

    private static void Union(Dictionary<string, string> parents, string a, string b)
    {
        var rootA = Root(parents, a);
        var rootB = Root(parents, b);
        if (string.Equals(rootA, rootB, StringComparison.Ordinal))
        {
            return;
        }

        if (ListingKey.Compare(rootA, rootB) <= 0)
        {
            parents[rootB] = rootA;
        }
        else
        {
            parents[rootA] = rootB;
        }
    }
}
