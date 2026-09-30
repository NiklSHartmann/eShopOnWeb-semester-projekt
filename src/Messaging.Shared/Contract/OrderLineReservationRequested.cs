namespace Messaging.Shared.Contracts;

// Published by the Splitter: one message per order line.
public sealed record OrderLineReservationRequested(
    int OrderId,          // correlation: which order this line belongs to
    int LineNumber,       // 1-based position of this line in the order
    int LineCount,        // total number of lines, so the Aggregator knows when it has them all
    int CatalogItemId,
    string ProductName,
    int CatalogTypeId,
    int Units);
