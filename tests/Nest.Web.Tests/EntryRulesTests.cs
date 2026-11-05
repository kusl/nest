using System.Text.Json;
using Nest.Web.Listings;

namespace Nest.Web.Tests;

public sealed class EntryRulesTests
{
    private static readonly Guid Id = Guid.Parse("01950000-0001-7000-8000-000000000001");

    [Fact]
    public void LinksAreKeyedByTheirListingAndStripped()
    {
        var entry = EntryRules.Build(Id, Drafts.Link("See https://www.zillow.com/homedetails/29747113_zpid/?utm=1 now", "address:13777-main", "bad"));
        Assert.NotNull(entry);
        Assert.Equal(EntryKinds.Link, entry.Kind);
        Assert.Equal(["address:13777-main", "zillow:29747113"], entry.Keys);
        Assert.Equal("https://www.zillow.com/homedetails/29747113_zpid/", entry.Url);
        Assert.Null(entry.Fields);
        Assert.Null(entry.Text);
        Assert.Equal("zillow:29747113", EntryRules.DerivedKey(entry));
        Assert.Null(EntryRules.Build(Id, Drafts.Link("no link here")));
    }

    [Fact]
    public void FactsAreNormalizedOrderedAndKeyedByAddress()
    {
        var entry = EntryRules.Build(Id, Drafts.Fact([], ("zip", "13777-1234"), ("price", "450k"), ("street", "2893 State Route 26"), ("price", "1"), ("pool", "yes")));
        Assert.NotNull(entry);
        Assert.Equal(["address:13777-2893-state-rte-26"], entry.Keys);
        Assert.Equal([new FieldValue("price", "450000"), new FieldValue("street", "2893 State Route 26"), new FieldValue("zip", "13777")], entry.Fields);
        Assert.Equal("address:13777-2893-state-rte-26", EntryRules.DerivedKey(entry));
    }

    [Fact]
    public void FactsNeedAKeyAndValidValues()
    {
        Assert.Null(EntryRules.Build(Id, Drafts.Fact([], ("beds", "3"))));
        Assert.Null(EntryRules.Build(Id, Drafts.Fact(["zillow:1"], ("beds", "lots"))));
        Assert.Null(EntryRules.Build(Id, Drafts.Fact(["zillow:1"])));
        Assert.Null(EntryRules.Build(Id, Drafts.Fact(["zillow:1"], ("pool", "yes"))));
        var cleared = EntryRules.Build(Id, Drafts.Fact(["zillow:1"], ("beds", " ")));
        Assert.Equal([new FieldValue("beds", "")], cleared?.Fields);
    }

    [Fact]
    public void CommentsNeedTextAndAKey()
    {
        var entry = EntryRules.Build(Id, Drafts.Comment("  Roof looks new 🏠\n\n\n\nGood  ", "zillow:1"));
        Assert.Equal("Roof looks new 🏠\n\nGood", entry?.Text);
        Assert.Null(EntryRules.Build(Id, Drafts.Comment("   ", "zillow:1")));
        Assert.Null(EntryRules.Build(Id, Drafts.Comment("hello")));
        Assert.Null(EntryRules.Build(Id, Drafts.Comment("hello", "bad")));
        Assert.Null(EntryRules.Build(Id, new EntryDraft("poll", ["zillow:1"], null, null, "x")));
    }

    [Fact]
    public void RecognizesNormalizedEntries()
    {
        var link = EntryRules.Build(Id, Drafts.Link("https://www.redfin.com/home/5"))!;
        var fact = EntryRules.Build(Id, Drafts.Fact(["zillow:1"], ("beds", "3")))!;
        var comment = EntryRules.Build(Id, Drafts.Comment("hi", "zillow:1"))!;
        Assert.True(EntryRules.IsNormalized(link));
        Assert.True(EntryRules.IsNormalized(fact));
        Assert.True(EntryRules.IsNormalized(comment));
        Assert.False(EntryRules.IsNormalized(null));
        Assert.False(EntryRules.IsNormalized(link with { Url = "https://www.redfin.com/home/5?x=1" }));
        Assert.False(EntryRules.IsNormalized(link with { Keys = ["zillow:9"] }));
        Assert.False(EntryRules.IsNormalized(link with { Text = "extra" }));
        Assert.False(EntryRules.IsNormalized(fact with { Fields = [new FieldValue("beds", "03")] }));
        Assert.False(EntryRules.IsNormalized(comment with { Text = " hi " }));
        Assert.False(EntryRules.IsNormalized(comment with { Keys = ["zillow:1", "redfin:2", "zillow:1"] }));
        Assert.True(EntryRules.IsNormalized(comment with { Keys = ["redfin:2", "zillow:1"] }));
    }

    [Fact]
    public void ComparesEntriesByValue()
    {
        var a = EntryRules.Build(Id, Drafts.Fact(["zillow:1"], ("beds", "3")))!;
        var b = EntryRules.Build(Id, Drafts.Fact(["zillow:1"], ("beds", "3")))!;
        Assert.True(EntryRules.Same(a, b));
        Assert.False(EntryRules.Same(a, b with { Fields = null }));
    }
}

public sealed class WireTests
{
    [Fact]
    public void ReadsEntriesFromTheBrowser()
    {
        using var json = JsonDocument.Parse("""[{"id":"01950000-0001-7000-8000-000000000001","kind":"comment","keys":["zillow:1"],"text":"hi"},{"id":"nope","kind":"comment","keys":[]},null]""");
        var wires = Wire.Read<EntryWire?[]>(json.RootElement);
        Assert.NotNull(wires);
        Assert.Equal(3, wires.Length);
        var entry = wires[0]?.ToEntry();
        Assert.Equal("hi", entry?.Text);
        Assert.Equal(["zillow:1"], entry?.Keys);
        Assert.Null(wires[1]?.ToEntry());
        Assert.Null(wires[2]);
    }

    [Fact]
    public void RejectsMalformedPayloads()
    {
        using var json = JsonDocument.Parse("""{"kind":42}""");
        Assert.Null(Wire.Read<EntryDraft>(json.RootElement));
        Assert.Null(new EntryWire("01950000-0001-7000-8000-000000000001", "fact", ["zillow:1"], null, [new FieldWire(null, "x")], null).ToEntry());
        Assert.Null(new EntryWire("01950000-0001-7000-8000-000000000001", "fact", ["zillow:1", null], null, null, null).ToEntry());
    }

    [Fact]
    public void WritesCamelCaseWithoutNulls()
    {
        var entry = new Entry(Guid.Parse("01950000-0001-7000-8000-000000000001"), EntryKinds.Comment, ["zillow:1"], Text: "hi");
        var json = Wire.Write(PostOutcome.From(new SubmitReply(SubmitResult.Accepted, entry, "zillow:1"))).GetRawText();
        Assert.Equal("""{"result":"accepted","entry":{"id":"01950000-0001-7000-8000-000000000001","kind":"comment","keys":["zillow:1"],"text":"hi"},"key":"zillow:1"}""", json);
    }

    [Fact]
    public void NamesResultsForTheBrowser()
    {
        Assert.Equal("accepted", PostOutcome.NameOf(SubmitResult.Accepted));
        Assert.Equal("exists", PostOutcome.NameOf(SubmitResult.Exists));
        Assert.Equal("invalid", PostOutcome.NameOf(SubmitResult.Invalid));
        Assert.Equal("tooFast", PostOutcome.NameOf(SubmitResult.TooFast));
        Assert.Equal("repeat", PostOutcome.NameOf(SubmitResult.Repeat));
    }
}
