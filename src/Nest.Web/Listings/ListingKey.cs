using System.Text;
using System.Text.RegularExpressions;

namespace Nest.Web.Listings;

internal static partial class ListingKey
{
    public const int MaxKeys = 8;
    public const int MaxToken = 100;
    public const string Zillow = "zillow";
    public const string Redfin = "redfin";
    public const string Realtor = "realtor";
    public const string Address = "address";
    public const string Url = "url";

    private static readonly string[] Order = [Zillow, Redfin, Realtor, Address, Url];

    private static readonly string[] UnitWords = ["apt", "apartment", "unit", "suite", "ste"];

    private static readonly Dictionary<string, string> StreetWords = new(StringComparer.Ordinal)
    {
        ["alley"] = "aly",
        ["avenue"] = "ave",
        ["av"] = "ave",
        ["boulevard"] = "blvd",
        ["circle"] = "cir",
        ["court"] = "ct",
        ["cove"] = "cv",
        ["crescent"] = "cres",
        ["creek"] = "crk",
        ["crossing"] = "xing",
        ["drive"] = "dr",
        ["expressway"] = "expy",
        ["freeway"] = "fwy",
        ["glen"] = "gln",
        ["heights"] = "hts",
        ["highway"] = "hwy",
        ["hill"] = "hl",
        ["hollow"] = "holw",
        ["junction"] = "jct",
        ["lane"] = "ln",
        ["loop"] = "loop",
        ["manor"] = "mnr",
        ["mount"] = "mt",
        ["mountain"] = "mtn",
        ["parkway"] = "pkwy",
        ["place"] = "pl",
        ["plaza"] = "plz",
        ["point"] = "pt",
        ["ridge"] = "rdg",
        ["road"] = "rd",
        ["route"] = "rte",
        ["rt"] = "rte",
        ["square"] = "sq",
        ["street"] = "st",
        ["terrace"] = "ter",
        ["trail"] = "trl",
        ["turnpike"] = "tpke",
        ["valley"] = "vly",
        ["view"] = "vw",
        ["village"] = "vlg",
        ["north"] = "n",
        ["south"] = "s",
        ["east"] = "e",
        ["west"] = "w",
        ["northeast"] = "ne",
        ["northwest"] = "nw",
        ["southeast"] = "se",
        ["southwest"] = "sw",
    };

    public static IReadOnlyList<string> Providers => Order;

    public static IComparer<string> Comparer { get; } = Comparer<string>.Create(Compare);

    public static string? ProviderOf(string? key)
    {
        var colon = key?.IndexOf(':', StringComparison.Ordinal) ?? -1;
        return colon > 0 ? key![..colon] : null;
    }

    public static string? TokenOf(string? key)
    {
        var colon = key?.IndexOf(':', StringComparison.Ordinal) ?? -1;
        return colon > 0 ? key![(colon + 1)..] : null;
    }

    public static bool IsValid(string? provider, string? token)
    {
        if (provider is null || token is null || token.Length > MaxToken)
        {
            return false;
        }

        return provider switch
        {
            Zillow or Redfin => NumericToken().IsMatch(token),
            Realtor => RealtorToken().IsMatch(token),
            Address => AddressToken().IsMatch(token),
            Url => HashToken().IsMatch(token),
            _ => false,
        };
    }

    public static bool IsValid(string? key) => IsValid(ProviderOf(key), TokenOf(key));

    public static string? Of(string provider, string? token) =>
        IsValid(provider, token) ? string.Concat(provider, ":", token) : null;

    public static int RankOf(string? key)
    {
        var provider = ProviderOf(key);
        var rank = provider is null ? -1 : Array.IndexOf(Order, provider);
        return rank < 0 ? Order.Length : rank;
    }

    public static int Compare(string? a, string? b)
    {
        var rank = RankOf(a) - RankOf(b);
        return rank != 0 ? rank : string.CompareOrdinal(a, b);
    }

    public static string ProviderLabel(string? provider) => provider switch
    {
        Zillow => "Zillow",
        Redfin => "Redfin",
        Realtor => "Realtor.com",
        Address => "Address",
        _ => "Link",
    };

    public static string Label(string key)
    {
        var provider = ProviderOf(key);
        var token = TokenOf(key) ?? "";
        return provider switch
        {
            Zillow or Redfin => string.Concat(ProviderLabel(provider), " ", token),
            Realtor => string.Concat("Realtor.com ", UpperAscii(token)),
            Address when token.Length > 6 => string.Concat(token[6..].Replace('-', ' '), ", ", token[..5]),
            _ => string.Concat("Link ", token.Length > 6 ? token[..6] : token),
        };
    }

    public static string Path(string key) => string.Concat("homes/", ProviderOf(key), "/", TokenOf(key));

    public static string[] Normalize(IEnumerable<string> derived, IEnumerable<string?>? anchors)
    {
        ArgumentNullException.ThrowIfNull(derived);
        var keys = new List<string>(MaxKeys);
        foreach (var key in derived)
        {
            AddKey(keys, key);
        }

        foreach (var key in anchors ?? [])
        {
            AddKey(keys, key);
        }

        keys.Sort(StringComparer.Ordinal);
        return [.. keys];
    }

    public static string? AddressOf(string? street, string? unit, string? zip)
    {
        if (zip is null || !ZipToken().IsMatch(zip))
        {
            return null;
        }

        var tokens = Words(UnitMarked(street));
        var split = -1;
        for (var i = 1; i < tokens.Count; i++)
        {
            if (UnitWords.Contains(tokens[i], StringComparer.Ordinal))
            {
                split = i;
                break;
            }
        }

        var streetTokens = (split > 0 ? tokens.GetRange(0, split) : tokens)
            .Select(token => StreetWords.TryGetValue(token, out var shortened) ? shortened : token)
            .ToList();
        if (streetTokens.Count == 0)
        {
            return null;
        }

        var unitInput = Words(UnitMarked(unit));
        var unitSource = unitInput.Count > 0 ? unitInput : split > 0 ? tokens.GetRange(split, tokens.Count - split) : new List<string>();
        var unitTokens = unitSource.Where(token => !UnitWords.Contains(token, StringComparer.Ordinal)).ToList();

        var parts = new List<string>(streetTokens.Count + unitTokens.Count + 2) { zip };
        parts.AddRange(streetTokens);
        if (unitTokens.Count > 0)
        {
            parts.Add("unit");
            parts.AddRange(unitTokens);
        }

        return Of(Address, string.Join('-', parts));
    }

    public static List<string> Words(string? text)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var c in text ?? "")
        {
            if (c is >= 'A' and <= 'Z')
            {
                current.Append((char)(c + 32));
            }
            else if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                current.Append(c);
            }
            else if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        return words;
    }

    public static string LowerAscii(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return string.Create(text.Length, text, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = c is >= 'A' and <= 'Z' ? (char)(c + 32) : c;
            }
        });
    }

    public static string UpperAscii(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return string.Create(text.Length, text, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = c is >= 'a' and <= 'z' ? (char)(c - 32) : c;
            }
        });
    }

    public static string Fnv1a64(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var hash = 0xcbf29ce484222325UL;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash = unchecked(hash * 0x100000001b3UL);
        }

        return hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddKey(List<string> keys, string? key)
    {
        if (keys.Count < MaxKeys && key is not null && IsValid(key) && !keys.Contains(key, StringComparer.Ordinal))
        {
            keys.Add(key);
        }
    }

    private static string UnitMarked(string? text) => text?.Replace("#", " unit ", StringComparison.Ordinal) ?? "";

    [GeneratedRegex(@"^[1-9][0-9]{0,11}\z")]
    private static partial Regex NumericToken();

    [GeneratedRegex(@"^m[0-9]{1,12}-[0-9]{1,12}\z")]
    private static partial Regex RealtorToken();

    [GeneratedRegex(@"^[0-9]{5}(?:-[a-z0-9]+)+\z")]
    private static partial Regex AddressToken();

    [GeneratedRegex(@"^[0-9a-f]{16}\z")]
    private static partial Regex HashToken();

    [GeneratedRegex(@"^[0-9]{5}\z")]
    private static partial Regex ZipToken();
}
