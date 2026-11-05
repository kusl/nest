using Nest.Web.Listings;

namespace Nest.Web.Tests;

public sealed class ListingKeyTests
{
    [Fact]
    public void LinksMatchTheSharedFixture()
    {
        foreach (var vector in Fixtures.Load<LinkVector[]>("links"))
        {
            var parsed = ListingLink.Parse(vector.Input);
            Assert.Equal((vector.Input, vector.Key, vector.Url), (vector.Input, parsed?.Key, parsed?.Url));
        }
    }

    [Fact]
    public void AddressesMatchTheSharedFixture()
    {
        foreach (var vector in Fixtures.Load<AddressVector[]>("addresses"))
        {
            Assert.Equal((vector.Street, vector.Unit, vector.Key), (vector.Street, vector.Unit, ListingKey.AddressOf(vector.Street, vector.Unit, vector.Zip)));
        }
    }

    [Theory]
    [InlineData("zillow:29747113", true)]
    [InlineData("zillow:029747113", false)]
    [InlineData("zillow:29747113\n", false)]
    [InlineData("redfin:1", true)]
    [InlineData("realtor:m1-2", true)]
    [InlineData("realtor:M1-2", false)]
    [InlineData("address:13777-main", true)]
    [InlineData("address:1377-main", false)]
    [InlineData("address:13777-Main", false)]
    [InlineData("url:0123456789abcdef", true)]
    [InlineData("url:0123", false)]
    [InlineData("other:1", false)]
    [InlineData(":1", false)]
    [InlineData(null, false)]
    public void ValidatesKeys(string? key, bool expected)
    {
        Assert.Equal(expected, ListingKey.IsValid(key));
    }

    [Fact]
    public void RanksProvidersBeforeAddressesAndLinks()
    {
        string[] keys = ["url:0123456789abcdef", "address:13777-main", "redfin:2", "zillow:9", "realtor:m1-2"];
        Assert.Equal(["zillow:9", "redfin:2", "realtor:m1-2", "address:13777-main", "url:0123456789abcdef"], keys.Order(ListingKey.Comparer));
    }

    [Fact]
    public void NormalizesKeyLists()
    {
        Assert.Equal(["redfin:2", "zillow:1"], ListingKey.Normalize(["zillow:1"], ["zillow:1", "bad", null, "redfin:2"]));
        var many = Enumerable.Range(1, 12).Select(i => $"redfin:{i}").ToArray();
        var keys = ListingKey.Normalize(["zillow:1"], many);
        Assert.Equal(ListingKey.MaxKeys, keys.Length);
        Assert.Contains("zillow:1", keys);
        Assert.Empty(ListingKey.Normalize([], null));
    }

    [Theory]
    [InlineData("zillow:29747113", "Zillow 29747113")]
    [InlineData("realtor:m1-2", "Realtor.com M1-2")]
    [InlineData("address:13777-2893-state-rte-26", "2893 state rte 26, 13777")]
    [InlineData("url:0123456789abcdef", "Link 012345")]
    public void LabelsKeys(string key, string expected)
    {
        Assert.Equal(expected, ListingKey.Label(key));
    }

    [Fact]
    public void BuildsPaths()
    {
        Assert.Equal("homes/zillow/1", ListingKey.Path("zillow:1"));
        Assert.Equal("zillow", ListingKey.ProviderOf("zillow:1"));
        Assert.Equal("1", ListingKey.TokenOf("zillow:1"));
        Assert.Null(ListingKey.ProviderOf("nothing"));
    }

    [Fact]
    public void HashesWithFnv1a()
    {
        Assert.Equal("cbf29ce484222325", ListingKey.Fnv1a64(""));
        Assert.Equal("af63dc4c8601ec8c", ListingKey.Fnv1a64("a"));
    }

    [Fact]
    public void ExtractsTheUrlFromSharedText()
    {
        Assert.Equal("https://a.example/x?y=1", ListingLink.ExtractUrl("look: https://a.example/x?y=1, nice"));
        Assert.Null(ListingLink.ExtractUrl("none"));
        Assert.Null(ListingLink.ExtractUrl(null));
        Assert.Null(ListingLink.ExtractUrl("https://a.example/" + new string('x', 3000)));
    }

    [Fact]
    public void SplitsWordsOnAnythingButAsciiLettersAndDigits()
    {
        Assert.Equal(["100", "w", "57th", "st"], ListingKey.Words("100 W. 57th-St"));
        Assert.Empty(ListingKey.Words(null));
    }
}
