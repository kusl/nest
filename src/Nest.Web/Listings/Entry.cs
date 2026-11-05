using System.Text.Json.Serialization;

namespace Nest.Web.Listings;

internal sealed record Entry(
    Guid Id,
    string Kind,
    IReadOnlyList<string> Keys,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Url = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<FieldValue>? Fields = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text = null);
