using Nest.Web.Listings;

namespace Nest.Web.Tests;

public sealed class ListingIndexTests
{
    [Fact]
    public void MergesEntriesThatShareAnyKey()
    {
        var expected = Fixtures.Listings().Listings;
        AssertMatches(expected, ListingIndex.Build(Fixtures.ListingEntries()));
    }

    [Fact]
    public void DoesNotDependOnEntryOrder()
    {
        var expected = Fixtures.Listings().Listings;
        var entries = Fixtures.ListingEntries();
        AssertMatches(expected, ListingIndex.Build(entries.AsEnumerable().Reverse()));
        AssertMatches(expected, ListingIndex.Build(entries.OrderBy(entry => entry.Text ?? entry.Kind, StringComparer.Ordinal)));
    }

    [Fact]
    public void FindsAListingByAnyOfItsKeys()
    {
        var listings = ListingIndex.Build(Fixtures.ListingEntries());
        Assert.Equal("zillow:29747113", ListingIndex.Find(listings, "address:13777-2893-state-rte-26")?.Key);
        Assert.Null(ListingIndex.Find(listings, "zillow:1"));
        Assert.Equal("Zillow · Redfin", listings[0].Sources);
    }

    [Fact]
    public void IgnoresEntriesWithoutKeys()
    {
        Assert.Empty(ListingIndex.Build([new Entry(Guid.CreateVersion7(), EntryKinds.Comment, [], Text: "lost")]));
    }

    private static void AssertMatches(IReadOnlyList<ListingVector> expected, IReadOnlyList<Listing> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            var want = expected[i];
            var have = actual[i];
            Assert.Equal(want.Key, have.Key);
            Assert.Equal(want.Keys, have.Keys);
            Assert.Equal(want.Links, have.Links.Select(link => link.Url));
            Assert.Equal(want.Fields.OrderBy(pair => pair.Key, StringComparer.Ordinal), have.Fields.OrderBy(pair => pair.Key, StringComparer.Ordinal));
            Assert.Equal(want.Comments, have.Comments.Select(comment => comment.Id.ToString("D")));
            Assert.Equal(want.Updated, have.Updated.ToString("D"));
            Assert.Equal(want.Title, have.Title);
        }
    }
}
