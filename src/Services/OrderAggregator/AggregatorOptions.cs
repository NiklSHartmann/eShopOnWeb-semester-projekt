namespace OrderAggregator;

public sealed class AggregatorOptions
{
    public const string SectionName = "Aggregator";

    public int TimeoutSeconds { get; set; } = 30;
    public int RelayIntervalSeconds { get; set; } = 2;

    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);
    public TimeSpan RelayInterval => TimeSpan.FromSeconds(RelayIntervalSeconds);
}
