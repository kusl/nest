using System.Text.RegularExpressions;

namespace Nest.Web.Listings;

internal sealed record ParsedLink(string Key, string Provider, string Url);

internal static partial class ListingLink
{
    public const int MaxUrl = 2048;
    private const string Trailing = ".,;:!?)]}";

    public static string? ExtractUrl(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var match = UrlPattern().Match(text);
        if (!match.Success)
        {
            return null;
        }

        var url = match.Value;
        var end = url.Length;
        while (end > 0 && Trailing.Contains(url[end - 1], StringComparison.Ordinal))
        {
            end--;
        }

        return end > 0 && end <= MaxUrl ? url[..end] : null;
    }

    public static ParsedLink? Parse(string? text)
    {
        var raw = ExtractUrl(text);
        var parts = raw is null ? null : PartsPattern().Match(raw);
        if (parts is null || !parts.Success)
        {
            return null;
        }

        var scheme = parts.Groups[1].Value.Length == 5 ? "https" : "http";
        var host = ListingKey.LowerAscii(parts.Groups[2].Value);
        var path = parts.Groups[3].Value.Length > 0 ? parts.Groups[3].Value : "/";
        if (!HostPattern().IsMatch(host))
        {
            return null;
        }

        var site = PortPattern().Replace(host, "");
        if (site.StartsWith("www.", StringComparison.Ordinal))
        {
            site = site[4..];
        }

        if (!site.Contains('.', StringComparison.Ordinal))
        {
            return null;
        }

        string? key = null;
        if (IsSite(site, "zillow.com"))
        {
            var match = ZillowPattern().Match(path);
            key = match.Success ? ListingKey.Of(ListingKey.Zillow, StripZeros(match.Groups[1].Value)) : null;
        }
        else if (IsSite(site, "redfin.com"))
        {
            var match = RedfinPattern().Match(path);
            key = match.Success ? ListingKey.Of(ListingKey.Redfin, StripZeros(match.Groups[1].Value)) : null;
        }
        else if (IsSite(site, "realtor.com"))
        {
            var match = RealtorPattern().Match(path);
            key = match.Success ? ListingKey.Of(ListingKey.Realtor, string.Concat("m", match.Groups[1].Value, "-", match.Groups[2].Value)) : null;
        }

        if (key is not null)
        {
            return new ParsedLink(key, ListingKey.ProviderOf(key)!, string.Concat("https://", host, path));
        }

        var canonical = string.Concat(site, path.TrimEnd('/'));
        return new ParsedLink(string.Concat(ListingKey.Url, ":", ListingKey.Fnv1a64(canonical)), ListingKey.Url, string.Concat(scheme, "://", host, path));
    }

    private static bool IsSite(string site, string domain) =>
        string.Equals(site, domain, StringComparison.Ordinal) || site.EndsWith(string.Concat(".", domain), StringComparison.Ordinal);

    private static string StripZeros(string digits)
    {
        var trimmed = digits.TrimStart('0');
        return trimmed.Length > 0 ? trimmed : "0";
    }

    [GeneratedRegex(@"[hH][tT][tT][pP][sS]?://[\x21\x23-\x26\x28-\x3B\x3D\x3F-\x7E]+")]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"^([hH][tT][tT][pP][sS]?)://([^/?#]+)([^?#]*)")]
    private static partial Regex PartsPattern();

    [GeneratedRegex(@"^[a-z0-9.-]+(?::[0-9]{1,5})?\z")]
    private static partial Regex HostPattern();

    [GeneratedRegex(@":[0-9]+\z")]
    private static partial Regex PortPattern();

    [GeneratedRegex(@"([0-9]+)_[zZ][pP][iI][dD]")]
    private static partial Regex ZillowPattern();

    [GeneratedRegex(@"/home/([0-9]+)")]
    private static partial Regex RedfinPattern();

    [GeneratedRegex(@"_[mM]([0-9]+)-([0-9]+)")]
    private static partial Regex RealtorPattern();
}
