namespace Messaging.Shared.Contracts;

// Published by a warehouse as the answer to one OrderLineReservationRequested.
public sealed record OrderLineReservationResult(
    int OrderId,
    int LineNumber,
    int LineCount,        // passed through so the Aggregator does not depend on having seen the request
    int CatalogItemId,
    int Units,
    string Warehouse,
    bool Reserved,
    string? Reason);      // why the reservation failed, null when Reserved is true
