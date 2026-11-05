namespace Nest.Web.Listings;

internal static class EntryRules
{
    public static Entry? Build(Guid id, string? kind, IEnumerable<string?>? anchors, string? url, IEnumerable<FieldWire?>? fields, string? text) => kind switch
    {
        EntryKinds.Link => BuildLink(id, anchors, url),
        EntryKinds.Fact => BuildFact(id, anchors, fields),
        EntryKinds.Comment => BuildComment(id, anchors, text),
        _ => null,
    };

    public static Entry? Build(Guid id, EntryDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return Build(id, draft.Kind, draft.Anchors, draft.Url, draft.Fields, draft.Text);
    }

    public static string? DerivedKey(Entry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Kind switch
        {
            EntryKinds.Link => ListingLink.Parse(entry.Url)?.Key,
            EntryKinds.Fact => AddressKey(entry.Fields),
            _ => null,
        };
    }

    public static bool IsNormalized(Entry? entry)
    {
        if (entry?.Keys is null)
        {
            return false;
        }

        var fields = entry.Fields?.Select(field => new FieldWire(field.Key, field.Value)).ToArray();
        var rebuilt = Build(entry.Id, entry.Kind, entry.Keys, entry.Url, fields, entry.Text);
        return rebuilt is not null && Same(rebuilt, entry);
    }

    public static bool Same(Entry a, Entry b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return a.Id == b.Id
            && string.Equals(a.Kind, b.Kind, StringComparison.Ordinal)
            && a.Keys.SequenceEqual(b.Keys, StringComparer.Ordinal)
            && string.Equals(a.Url, b.Url, StringComparison.Ordinal)
            && string.Equals(a.Text, b.Text, StringComparison.Ordinal)
            && (a.Fields is null ? b.Fields is null : b.Fields is not null && a.Fields.SequenceEqual(b.Fields));
    }

    private static Entry? BuildLink(Guid id, IEnumerable<string?>? anchors, string? url)
    {
        var parsed = ListingLink.Parse(url);
        if (parsed is null)
        {
            return null;
        }

        return new Entry(id, EntryKinds.Link, ListingKey.Normalize(new[] { parsed.Key }, anchors), Url: parsed.Url);
    }

    private static Entry? BuildFact(Guid id, IEnumerable<string?>? anchors, IEnumerable<FieldWire?>? fields)
    {
        if (fields is null)
        {
            return null;
        }

        var values = new string?[ListingFields.All.Count];
        foreach (var field in fields.Take(ListingFields.All.Count * 2))
        {
            if (field?.Key is not { } key)
            {
                return null;
            }

            var index = ListingFields.OrderOf(key);
            if (index < 0 || values[index] is not null)
            {
                continue;
            }

            if (!ListingFields.TryNormalize(key, field.Value, out var value))
            {
                return null;
            }

            values[index] = value;
        }

        var normalized = new List<FieldValue>(values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] is { } value)
            {
                normalized.Add(new FieldValue(ListingFields.All[i].Key, value));
            }
        }

        if (normalized.Count == 0)
        {
            return null;
        }

        var derived = AddressKey(normalized);
        var keys = ListingKey.Normalize(derived is null ? Array.Empty<string>() : new[] { derived }, anchors);
        return keys.Length == 0 ? null : new Entry(id, EntryKinds.Fact, keys, Fields: normalized);
    }

    private static Entry? BuildComment(Guid id, IEnumerable<string?>? anchors, string? text)
    {
        var normalized = CommentText.Normalize(text);
        if (normalized.Length == 0)
        {
            return null;
        }

        var keys = ListingKey.Normalize(Array.Empty<string>(), anchors);
        return keys.Length == 0 ? null : new Entry(id, EntryKinds.Comment, keys, Text: normalized);
    }

    private static string? AddressKey(IReadOnlyList<FieldValue>? fields)
    {
        if (fields is null)
        {
            return null;
        }

        string? street = null;
        string? unit = null;
        string? zip = null;
        foreach (var field in fields)
        {
            switch (field.Key)
            {
                case ListingFields.Street:
                    street = field.Value;
                    break;
                case ListingFields.Unit:
                    unit = field.Value;
                    break;
                case ListingFields.Zip:
                    zip = field.Value;
                    break;
            }
        }

        return string.IsNullOrEmpty(street) || string.IsNullOrEmpty(zip) ? null : ListingKey.AddressOf(street, unit, zip);
    }
}
