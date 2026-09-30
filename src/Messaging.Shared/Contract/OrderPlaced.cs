namespace Messaging.Shared.Contracts;

// Published by the monolith when an order has been saved.
public sealed record OrderPlaced(
    int OrderId,
    string BuyerId,
    DateTimeOffset OrderDate,
    IReadOnlyList<OrderPlacedLine> Lines);

public sealed record OrderPlacedLine(
    int CatalogItemId,
    string ProductName,
    int CatalogTypeId,
    decimal UnitPrice,
    int Units);
