using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Nest.Web.Listings;

internal enum FieldKind
{
    Money,
    Count,
    Decimal,
    Integer,
    Choice,
    State,
    Zip,
    Text,
}

internal sealed record ListingField(string Key, string Label, FieldKind Kind, int MaxLength = 64, long Min = 0, long Max = 0, IReadOnlyList<string>? Options = null, string? Hint = null);

internal static partial class ListingFields
{
    public const string Price = "price";
    public const string Status = "status";
    public const string Beds = "beds";
    public const string Baths = "baths";
    public const string Sqft = "sqft";
    public const string Year = "year";
    public const string Street = "street";
    public const string Unit = "unit";
    public const string City = "city";
    public const string State = "state";
    public const string Zip = "zip";

    public static IReadOnlyList<string> StatusOptions { get; } = ["For sale", "Coming soon", "Pending", "Sold", "For rent", "Off market"];

    public static IReadOnlyList<string> TypeOptions { get; } = ["House", "Condo", "Townhouse", "Multi-family", "Manufactured", "Land", "Other"];

    public static IReadOnlyList<string> AddressKeys { get; } = [Street, Unit, City, State, Zip];

    public static IReadOnlyList<ListingField> All { get; } =
    [
        new(Price, "Price", FieldKind.Money, Hint: "450000 or 450k"),
        new(Status, "Status", FieldKind.Choice, Options: StatusOptions),
        new("type", "Type", FieldKind.Choice, Options: TypeOptions),
        new(Beds, "Bedrooms", FieldKind.Count),
        new(Baths, "Bathrooms", FieldKind.Decimal, Hint: "2.5"),
        new(Sqft, "Square feet", FieldKind.Integer, Min: 1, Max: 10_000_000),
        new("lot", "Lot size", FieldKind.Text, 40, Hint: "0.25 acres"),
        new(Year, "Year built", FieldKind.Integer, Min: 1600, Max: 2100),
        new("hoa", "HOA per month", FieldKind.Money),
        new("taxes", "Taxes per year", FieldKind.Money),
        new("parking", "Parking", FieldKind.Text, 60),
        new("heating", "Heating and cooling", FieldKind.Text, 60),
        new("mls", "MLS number", FieldKind.Text, 40),
        new(Street, "Street address", FieldKind.Text, 120),
        new(Unit, "Unit", FieldKind.Text, 20),
        new(City, "City", FieldKind.Text, 60),
        new(State, "State", FieldKind.State),
        new(Zip, "ZIP code", FieldKind.Zip),
    ];

    private static readonly Dictionary<string, int> Order = All
        .Select((field, index) => (field.Key, index))
        .ToDictionary(pair => pair.Key, pair => pair.index, StringComparer.Ordinal);

    public static ListingField? Find(string? key) =>
        key is not null && Order.TryGetValue(key, out var index) ? All[index] : null;

    public static int OrderOf(string key) => Order.TryGetValue(key, out var index) ? index : -1;

    public static string CleanText(string? input, int max)
    {
        var builder = new StringBuilder(Math.Min(input?.Length ?? 0, max + 1));
        foreach (var c in input ?? "")
        {
            if (IsSpace(c))
            {
                if (builder.Length > 0 && builder[^1] != ' ')
                {
                    builder.Append(' ');
                }
            }
            else if (c > '\u001f' && (c < '\u007f' || c > '\u009f'))
            {
                builder.Append(c);
            }
        }

        if (builder.Length > 0 && builder[^1] == ' ')
        {
            builder.Length--;
        }

        if (builder.Length > max)
        {
            var cut = max;
            if (char.IsHighSurrogate(builder[cut - 1]))
            {
                cut--;
            }

            builder.Length = cut;
            if (builder.Length > 0 && builder[^1] == ' ')
            {
                builder.Length--;
            }
        }

        return builder.ToString();
    }

    public static bool TryNormalize(string? key, string? input, out string value)
    {
        value = "";
        var field = Find(key);
        if (field is null)
        {
            return false;
        }

        var text = CleanText(input, field.Kind == FieldKind.Text ? field.MaxLength : 64);
        if (text.Length == 0)
        {
            return true;
        }

        var normalized = field.Kind switch
        {
            FieldKind.Money => Money(text),
            FieldKind.Count => CountPattern().IsMatch(text) ? StripZeros(text) : null,
            FieldKind.Decimal => DecimalValue(text),
            FieldKind.Integer => IntegerValue(text, field),
            FieldKind.Choice => field.Options?.FirstOrDefault(option => string.Equals(ListingKey.LowerAscii(option), ListingKey.LowerAscii(text), StringComparison.Ordinal)),
            FieldKind.State => StatePattern().IsMatch(text) ? ListingKey.UpperAscii(text) : null,
            FieldKind.Zip => ZipValue(text),
            _ => text,
        };

        if (normalized is null)
        {
            return false;
        }

        value = normalized;
        return true;
    }

    public static string Display(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
        {
            return "";
        }

        return Find(key)?.Kind switch
        {
            FieldKind.Money => string.Concat("$", Grouped(value)),
            FieldKind.Integer when !string.Equals(key, Year, StringComparison.Ordinal) => Grouped(value),
            _ => value,
        };
    }

    public static string Summary(IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var parts = new List<string>(5);
        if (fields.TryGetValue(Price, out var price))
        {
            parts.Add(Display(Price, price));
        }

        if (fields.TryGetValue(Beds, out var beds))
        {
            parts.Add(string.Concat(beds, " bd"));
        }

        if (fields.TryGetValue(Baths, out var baths))
        {
            parts.Add(string.Concat(baths, " ba"));
        }

        if (fields.TryGetValue(Sqft, out var sqft))
        {
            parts.Add(string.Concat(Display(Sqft, sqft), " sq ft"));
        }

        if (fields.TryGetValue(Status, out var status))
        {
            parts.Add(status);
        }

        return string.Join(" · ", parts);
    }

    public static string Place(IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var city = string.Join(", ", new[] { fields.GetValueOrDefault(City), fields.GetValueOrDefault(State) }.Where(part => !string.IsNullOrEmpty(part)));
        return string.Join(' ', new[] { city, fields.GetValueOrDefault(Zip) }.Where(part => !string.IsNullOrEmpty(part)));
    }

    public static string StreetLine(IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (!fields.TryGetValue(Street, out var street))
        {
            return "";
        }

        return fields.TryGetValue(Unit, out var unit)
            ? string.Concat(street, ", unit ", UnitPrefix().Replace(unit, ""))
            : street;
    }

    private static bool IsSpace(char c) =>
        c is >= '\u0009' and <= '\u000d' or ' ' or ' ' or ' ' or >= ' ' and <= ' ' or ' ' or ' ' or '　';

    private static string StripZeros(string digits)
    {
        var trimmed = digits.TrimStart('0');
        return trimmed.Length > 0 ? trimmed : "0";
    }

    private static string? Money(string text)
    {
        var match = MoneyPattern().Match(MoneyNoise().Replace(text, ""));
        if (!match.Success)
        {
            return null;
        }

        var suffix = match.Groups[3].Value;
        var places = suffix.Length == 0 ? 0 : suffix is "k" or "K" ? 3 : 6;
        var fraction = match.Groups[2].Value;
        if (places == 0 && fraction.AsSpan().ContainsAnyExcept('0'))
        {
            return null;
        }

        if (places > 0 && fraction.Length > places)
        {
            return null;
        }

        var value = StripZeros(places > 0 ? string.Concat(match.Groups[1].Value, fraction.PadRight(places, '0')) : match.Groups[1].Value);
        return value.Length <= 11 ? value : null;
    }

    private static string? DecimalValue(string text)
    {
        var match = DecimalPattern().Match(text);
        if (!match.Success)
        {
            return null;
        }

        var whole = StripZeros(match.Groups[1].Value);
        var fraction = match.Groups[2].Value.TrimEnd('0');
        return fraction.Length > 0 ? string.Concat(whole, ".", fraction) : whole;
    }

    private static string? IntegerValue(string text, ListingField field)
    {
        var cleaned = IntegerNoise().Replace(text, "");
        if (!IntegerPattern().IsMatch(cleaned))
        {
            return null;
        }

        var value = StripZeros(cleaned);
        var number = long.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
        return number >= field.Min && number <= field.Max ? value : null;
    }

    private static string? ZipValue(string text)
    {
        var match = ZipPattern().Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string Grouped(string digits)
    {
        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return digits;
        }

        return number.ToString("#,0", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"[ ,$_]")]
    private static partial Regex MoneyNoise();

    [GeneratedRegex(@"[ ,_]")]
    private static partial Regex IntegerNoise();

    [GeneratedRegex(@"^([0-9]+)(?:\.([0-9]+))?([kKmM])?\z")]
    private static partial Regex MoneyPattern();

    [GeneratedRegex(@"^[0-9]{1,2}\z")]
    private static partial Regex CountPattern();

    [GeneratedRegex(@"^([0-9]{1,2})(?:\.([0-9]{1,2}))?\z")]
    private static partial Regex DecimalPattern();

    [GeneratedRegex(@"^[0-9]{1,8}\z")]
    private static partial Regex IntegerPattern();

    [GeneratedRegex(@"^[A-Za-z]{2}\z")]
    private static partial Regex StatePattern();

    [GeneratedRegex(@"^([0-9]{5})(?:-?[0-9]{4})?\z")]
    private static partial Regex ZipPattern();

    [GeneratedRegex(@"^(?:unit|apt|apartment|suite|ste|#)\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnitPrefix();
}
