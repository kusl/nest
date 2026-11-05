using Nest.Web.Listings;

namespace Nest.Web.Tests;

public sealed class ListingFieldsTests
{
    [Fact]
    public void FieldsMatchTheSharedFixture()
    {
        foreach (var vector in Fixtures.Load<FieldVector[]>("fields"))
        {
            var ok = ListingFields.TryNormalize(vector.Key, vector.Input, out var value);
            Assert.Equal((vector.Key, vector.Input, vector.Ok, vector.Ok ? vector.Value : ""), (vector.Key, vector.Input, ok, value));
            if (ok)
            {
                Assert.True(ListingFields.TryNormalize(vector.Key, value, out var again));
                Assert.Equal(value, again);
            }
        }
    }

    [Fact]
    public void KeysAreUniqueAndOrdered()
    {
        Assert.Equal(ListingFields.All.Count, ListingFields.All.Select(field => field.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(0, ListingFields.OrderOf("price"));
        Assert.Equal(-1, ListingFields.OrderOf("pool"));
        Assert.Null(ListingFields.Find(null));
    }

    [Fact]
    public void CleansText()
    {
        Assert.Equal("a b", ListingFields.CleanText(" a \n\n b ", 10));
        Assert.Equal("abc", ListingFields.CleanText("abcdef", 3));
        Assert.Equal("ab", ListingFields.CleanText("ab cd", 3));
        Assert.Equal("", ListingFields.CleanText(null, 3));
    }

    [Fact]
    public void FormatsValues()
    {
        Assert.Equal("$1,250,000", ListingFields.Display("price", "1250000"));
        Assert.Equal("1,800", ListingFields.Display("sqft", "1800"));
        Assert.Equal("1925", ListingFields.Display("year", "1925"));
        Assert.Equal("3", ListingFields.Display("beds", "3"));
        Assert.Equal("", ListingFields.Display("beds", ""));
    }

    [Fact]
    public void SummarizesAHome()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["price"] = "450000",
            ["beds"] = "3",
            ["baths"] = "2.5",
            ["sqft"] = "1800",
            ["status"] = "Pending",
            ["street"] = "1 Main St",
            ["unit"] = "Apt 4",
            ["city"] = "Glen Aubrey",
            ["state"] = "NY",
            ["zip"] = "13777",
        };
        Assert.Equal("$450,000 · 3 bd · 2.5 ba · 1,800 sq ft · Pending", ListingFields.Summary(fields));
        Assert.Equal("Glen Aubrey, NY 13777", ListingFields.Place(fields));
        Assert.Equal("1 Main St, unit 4", ListingFields.StreetLine(fields));
        var empty = new Dictionary<string, string>(StringComparer.Ordinal);
        Assert.Equal("", ListingFields.Summary(empty));
        Assert.Equal("", ListingFields.Place(empty));
        Assert.Equal("", ListingFields.StreetLine(empty));
    }
}
