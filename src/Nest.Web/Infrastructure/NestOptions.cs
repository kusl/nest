namespace Nest.Web.Infrastructure;

internal sealed class NestOptions
{
    public const string SectionName = "Nest";

    public IList<string> AllowedOrigins { get; } = [];

    public int MaxConnectionsPerClient { get; set; } = 8;

    public int MaxConnections { get; set; } = 80;

    public int RequestsPerSecond { get; set; } = 10;

    public int RequestBurst { get; set; } = 120;

    public int Capacity { get; set; } = 1000;

    public double PostBurst { get; set; } = 6;

    public double PostsPerSecond { get; set; } = 0.5;

    public bool IsValid() =>
        MaxConnectionsPerClient > 0
        && MaxConnections >= MaxConnectionsPerClient
        && RequestsPerSecond > 0
        && RequestBurst > 0
        && Capacity is >= 1 and <= 20_000
        && PostBurst >= 1
        && PostsPerSecond > 0;
}
