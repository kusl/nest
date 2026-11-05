using System.Globalization;

namespace Nest.Web.Listings;

internal static class EntryTime
{
    public static string Iso(Guid id) =>
        Uuid7.Timestamp(id).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public static string Label(Guid id) =>
        Uuid7.Timestamp(id).UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
