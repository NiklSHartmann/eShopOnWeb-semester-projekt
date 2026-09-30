namespace OrderAggregator.Data;

// One warehouse answer. The primary key (OrderId, LineNumber) makes duplicates impossible at the database level.
public class LineResult
{
    private LineResult()
    {
        // Used by EF Core.
    }

    public LineResult(int lineNumber, int catalogItemId, int units, string warehouse,
        bool reserved, string? reason, DateTimeOffset receivedAt)
    {
        LineNumber = lineNumber;
        CatalogItemId = catalogItemId;
        Units = units;
        Warehouse = warehouse;
        Reserved = reserved;
        Reason = reason;
        ReceivedAt = receivedAt;
    }

    public int OrderId { get; private set; }
    public int LineNumber { get; private set; }
    public int CatalogItemId { get; private set; }
    public int Units { get; private set; }
    public string Warehouse { get; private set; } = string.Empty;
    public bool Reserved { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
}
