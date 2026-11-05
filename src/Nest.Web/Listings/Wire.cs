using System.Text.Json;

namespace Nest.Web.Listings;

internal sealed record FieldWire(string? Key, string? Value);

internal sealed record EntryWire(string? Id, string? Kind, IReadOnlyList<string?>? Keys, string? Url, IReadOnlyList<FieldWire?>? Fields, string? Text)
{
    public Entry? ToEntry()
    {
        if (!Guid.TryParseExact(Id, "D", out var id) || Kind is null || Keys is null)
        {
            return null;
        }

        var keys = new List<string>(Keys.Count);
        foreach (var key in Keys)
        {
            if (key is null)
            {
                return null;
            }

            keys.Add(key);
        }

        List<FieldValue>? fields = null;
        if (Fields is not null)
        {
            fields = new List<FieldValue>(Fields.Count);
            foreach (var field in Fields)
            {
                if (field?.Key is null || field.Value is null)
                {
                    return null;
                }

                fields.Add(new FieldValue(field.Key, field.Value));
            }
        }

        return new Entry(id, Kind, keys, Url, fields, Text);
    }
}

internal sealed record EntryDraft(string? Kind, IReadOnlyList<string?>? Anchors, string? Url, IReadOnlyList<FieldWire?>? Fields, string? Text);

internal sealed record PostOutcome(string Result, Entry? Entry, string? Key)
{
    public static PostOutcome From(SubmitReply reply)
    {
        ArgumentNullException.ThrowIfNull(reply);
        return new PostOutcome(NameOf(reply.Result), reply.Entry, reply.Key);
    }

    public static string NameOf(SubmitResult result) => result switch
    {
        SubmitResult.Accepted => "accepted",
        SubmitResult.Exists => "exists",
        SubmitResult.TooFast => "tooFast",
        SubmitResult.Repeat => "repeat",
        _ => "invalid",
    };
}

internal static class Wire
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);

    public static T? Read<T>(JsonElement element)
        where T : class
    {
        try
        {
            return element.Deserialize<T>(Options);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public static JsonElement Write<T>(T value) => JsonSerializer.SerializeToElement(value, Options);
}
