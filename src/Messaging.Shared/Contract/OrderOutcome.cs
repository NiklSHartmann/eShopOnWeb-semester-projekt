namespace Messaging.Shared.Contracts;

// Published by the Aggregator when all results are in and every line was reserved.
public sealed record OrderConfirmed(int OrderId, IReadOnlyList<OrderLineOutcome> Lines);

// Published by the Aggregator when at least one line failed, or the order timed out.
// Lines includes the reserved ones, so a later compensating step knows what to release.
public sealed record OrderRejected(int OrderId, string Reason, bool TimedOut, IReadOnlyList<OrderLineOutcome> Lines);

public sealed record OrderLineOutcome(
    int LineNumber,
    int CatalogItemId,
    int Units,
    string Warehouse,
    bool Reserved,
    string? Reason);
