namespace Nest.Web.Listings;

internal enum SubmitResult
{
    Accepted,
    Exists,
    Invalid,
    TooFast,
    Repeat,
}

internal sealed record SubmitReply(SubmitResult Result, Entry? Entry = null, string? Key = null);
