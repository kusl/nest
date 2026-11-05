using Nest.Web.Infrastructure;
using Nest.Web.Listings;

namespace Nest.Web.Tests;

public sealed class NestServiceTests
{
    private const string Zillow = "https://www.zillow.com/homedetails/29747113_zpid/";
    private static readonly DateTimeOffset Start = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private readonly ManualTimeProvider _time = new(Start);

    [Fact]
    public void StampsEntriesWithTheServerClockInOrder()
    {
        var nest = Create();
        var first = nest.Submit(Drafts.Link(Zillow), "a");
        var second = nest.Submit(Drafts.Comment("hi", "zillow:29747113"), "a");
        Assert.Equal(SubmitResult.Accepted, first.Result);
        Assert.Equal("zillow:29747113", first.Key);
        Assert.Equal(Start, Uuid7.Timestamp(first.Entry!.Id));
        Assert.True(second.Entry!.Id.CompareTo(first.Entry.Id) > 0);
        Assert.Equal([second.Entry.Id, first.Entry.Id], nest.Entries.Select(entry => entry.Id));
    }

    [Fact]
    public void RejectsInvalidDrafts()
    {
        var nest = Create();
        Assert.Equal(SubmitResult.Invalid, nest.Submit(null, "a").Result);
        Assert.Equal(SubmitResult.Invalid, nest.Submit(Drafts.Link("nothing"), "a").Result);
        Assert.Equal(SubmitResult.Invalid, nest.Submit(Drafts.Comment("orphan"), "a").Result);
        Assert.Empty(nest.Entries);
    }

    [Fact]
    public void ReturnsTheExistingHomeForAKnownLink()
    {
        var nest = Create();
        nest.Submit(Drafts.Link(Zillow), "a");
        var again = nest.Submit(Drafts.Link(Zillow + "?utm_source=share"), "b");
        Assert.Equal(SubmitResult.Exists, again.Result);
        Assert.Equal("zillow:29747113", again.Key);
        Assert.Null(again.Entry);
        var joined = nest.Submit(Drafts.Link(Zillow, "redfin:5"), "b");
        Assert.Equal(SubmitResult.Accepted, joined.Result);
        Assert.Single(nest.Listings);
    }

    [Fact]
    public void KeysAnAddressFactByItsAddress()
    {
        var reply = Create().Submit(Drafts.Fact([], ("street", "1 Main Street"), ("zip", "13777")), "a");
        Assert.Equal(SubmitResult.Accepted, reply.Result);
        Assert.Equal("address:13777-1-main-st", reply.Key);
    }

    [Fact]
    public void RefusesRecentRepeats()
    {
        var nest = Create();
        Assert.Equal(SubmitResult.Accepted, nest.Submit(Drafts.Comment("same", "zillow:1"), "a").Result);
        Assert.Equal(SubmitResult.Repeat, nest.Submit(Drafts.Comment("same", "zillow:1"), "b").Result);
        Assert.Equal(SubmitResult.Accepted, nest.Submit(Drafts.Comment("same", "zillow:2"), "b").Result);
    }

    [Fact]
    public void RateLimitsEachClient()
    {
        var nest = Create(o =>
        {
            o.PostBurst = 2;
            o.PostsPerSecond = 0.5;
        });
        Assert.Equal(SubmitResult.Accepted, nest.Submit(Drafts.Comment("one", "zillow:1"), "a").Result);
        Assert.Equal(SubmitResult.Accepted, nest.Submit(Drafts.Comment("two", "zillow:1"), "a").Result);
        Assert.Equal(SubmitResult.TooFast, nest.Submit(Drafts.Comment("three", "zillow:1"), "a").Result);
        Assert.Equal(SubmitResult.Accepted, nest.Submit(Drafts.Comment("four", "zillow:1"), "b").Result);
        _time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(SubmitResult.Accepted, nest.Submit(Drafts.Comment("five", "zillow:1"), "a").Result);
    }

    [Fact]
    public void KeepsOnlyTheNewestEntries()
    {
        var nest = Create(o => o.Capacity = 3);
        for (var i = 0; i < 5; i++)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            nest.Submit(Drafts.Comment($"c{i}", "zillow:1"), $"client {i}");
        }

        Assert.Equal(["c4", "c3", "c2"], nest.Entries.Select(entry => entry.Text));
    }

    [Fact]
    public void AcceptsOffersFromBeforeItStarted()
    {
        var nest = Create();
        var old = Comment(Start.AddDays(-3), "kept by a browser");
        Assert.Equal(1, nest.Offer([old]));
        Assert.Equal(0, nest.Offer([old]));
        Assert.Equal(old.Id, Assert.Single(nest.Entries).Id);
    }

    [Fact]
    public void RejectsOffersFromTheFutureOrAfterItStarted()
    {
        var nest = Create();
        Assert.Equal(0, nest.Offer([Comment(Start.AddMinutes(5), "from the future")]));
        _time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(0, nest.Offer([Comment(Start.AddMinutes(1), "forged")]));
        Assert.Empty(nest.Entries);
    }

    [Fact]
    public void RejectsOffersThatAreNotNormalized()
    {
        var nest = Create();
        var at = Start.AddDays(-1);
        Assert.Equal(0, nest.Offer([Comment(at, " padded ")]));
        Assert.Equal(0, nest.Offer([new Entry(Guid.CreateVersion7(at), EntryKinds.Comment, ["bad"], Text: "x")]));
        Assert.Equal(0, nest.Offer([new Entry(Guid.NewGuid(), EntryKinds.Comment, ["zillow:1"], Text: "x")]));
        Assert.Equal(0, nest.Offer([new Entry(Guid.CreateVersion7(at), EntryKinds.Link, ["zillow:1"], Url: "https://www.zillow.com/homedetails/2_zpid/")]));
        Assert.Empty(nest.Entries);
    }

    [Fact]
    public void KeepsItsOwnEntriesWhenFullAndOffersAreOlder()
    {
        var nest = Create(o => o.Capacity = 2);
        nest.Submit(Drafts.Comment("new one", "zillow:1"), "a");
        nest.Submit(Drafts.Comment("new two", "zillow:1"), "b");
        Assert.Equal(0, nest.Offer([Comment(Start.AddDays(-1), "older")]));
        Assert.Equal(["new two", "new one"], nest.Entries.Select(entry => entry.Text));
    }

    [Fact]
    public void SubscribersGetIdsThenDeltas()
    {
        var nest = Create(o => o.Capacity = 1);
        var first = nest.Submit(Drafts.Comment("first", "zillow:1"), "a").Entry!;
        var recorder = new NestRecorder();
        nest.Subscribe(recorder);
        _time.Advance(TimeSpan.FromSeconds(1));
        var second = nest.Submit(Drafts.Comment("second", "zillow:1"), "b").Entry!;

        Assert.Equal(2, recorder.Updates.Count);
        var snapshot = recorder.Updates[0];
        Assert.Equal("snapshot", snapshot.Kind);
        Assert.Equal(1, snapshot.Capacity);
        Assert.Equal([first.Id], snapshot.Ids);
        Assert.Empty(snapshot.Added);
        var delta = recorder.Updates[1];
        Assert.Equal("delta", delta.Kind);
        Assert.Equal(snapshot.Version, delta.Previous);
        Assert.Equal(second.Id, Assert.Single(delta.Added).Id);
        Assert.Equal([first.Id], delta.Removed);
        Assert.True(nest.IsSubscribed(recorder));
        nest.Unsubscribe(recorder);
        Assert.False(nest.IsSubscribed(recorder));
    }

    [Fact]
    public void DropsSubscribersThatCannotKeepUp()
    {
        var nest = Create();
        var refusing = new NestRecorder(accept: false);
        nest.Subscribe(refusing);
        Assert.False(nest.IsSubscribed(refusing));
    }

    [Fact]
    public void FindsEntriesById()
    {
        var nest = Create();
        var entry = nest.Submit(Drafts.Comment("find me", "zillow:1"), "a").Entry!;
        Assert.Equal([entry], nest.Find([entry.Id, Guid.NewGuid()]));
        Assert.Empty(nest.Find([]));
    }

    [Fact]
    public void BuildsListingsOncePerChange()
    {
        var nest = Create();
        nest.Submit(Drafts.Link(Zillow), "a");
        var listings = nest.Listings;
        Assert.Same(listings, nest.Listings);
        Assert.Equal("zillow:29747113", nest.FindListing("zillow:29747113")?.Key);
        nest.Submit(Drafts.Comment("new", "zillow:29747113"), "a");
        Assert.NotSame(listings, nest.Listings);
        Assert.Single(nest.FindListing("zillow:29747113")!.Comments);
        Assert.Equal(2, nest.Version);
    }

    private static Entry Comment(DateTimeOffset at, string text) =>
        new(Guid.CreateVersion7(at), EntryKinds.Comment, ["zillow:1"], Text: text);

    private NestService Create(Action<NestOptions>? configure = null) => new(_time, Limits.Create(configure));
}

public sealed class CommentTextTests
{
    [Fact]
    public void KeepsEmojiAndCutsOnGraphemes()
    {
        Assert.Equal("👍🏽 ok", CommentText.Normalize(" 👍🏽 ok "));
        var repeated = string.Concat(Enumerable.Repeat("👍🏽", CommentText.MaxLength));
        var cut = CommentText.Normalize(repeated);
        Assert.True(cut.Length <= CommentText.MaxLength);
        Assert.Equal(0, cut.Length % 4);
        Assert.True(CommentText.IsNormalized(cut));
        Assert.False(CommentText.IsNormalized(" x"));
        Assert.Equal("", CommentText.Normalize("​"));
    }
}
