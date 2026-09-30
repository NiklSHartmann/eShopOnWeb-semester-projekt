namespace OrderAggregator.Data;

public enum AggregationStatus
{
    Pending,
    Confirmed,
    Rejected,
    TimedOut
}

public enum AddResultStep
{
    Waiting,    // stored, still missing results
    Completed,  // this result completed the aggregation
    Duplicate,  // this line was already recorded; nothing changed
    Late        // arrived after the order was already decided
}

// The aggregate: all state for one order being aggregated. All rules live here, not in the handlers.
public class OrderAggregation
{
    private readonly List<LineResult> _lines = new();

    private OrderAggregation()
    {
        // Used by EF Core when loading from the database.
    }

    public OrderAggregation(int orderId, int lineCount, DateTimeOffset now, TimeSpan timeout)
    {
        OrderId = orderId;
        LineCount = lineCount;
        Status = AggregationStatus.Pending;
        CreatedAt = now;
        TimeoutAt = now + timeout;
    }

    public int OrderId { get; private set; }
    public int LineCount { get; private set; }
    public int ReceivedCount { get; private set; }
    public AggregationStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset TimeoutAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? OutcomePublishedAt { get; private set; }

    // Concurrency token: SQL Server changes it on every UPDATE of this row.
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public IReadOnlyCollection<LineResult> Lines => _lines.AsReadOnly();

    public bool IsDecided => Status != AggregationStatus.Pending;

    public AddResultStep AddResult(int lineNumber, int catalogItemId, int units, string warehouse,
        bool reserved, string? reason, DateTimeOffset now)
    {
        if (_lines.Any(l => l.LineNumber == lineNumber))
        {
            return AddResultStep.Duplicate;
        }

        _lines.Add(new LineResult(lineNumber, catalogItemId, units, warehouse, reserved, reason, now));

        // Changing a column on THIS row makes EF Core issue an UPDATE with "WHERE RowVersion = @original",
        // so two handlers that add results to the same order at the same time cannot both succeed.
        ReceivedCount++;

        if (IsDecided)
        {
            return AddResultStep.Late;
        }

        if (ReceivedCount < LineCount)
        {
            return AddResultStep.Waiting;
        }

        var failed = _lines.Count(l => !l.Reserved);
        Status = failed == 0 ? AggregationStatus.Confirmed : AggregationStatus.Rejected;
        RejectionReason = failed == 0 ? null : $"{failed} of {LineCount} lines could not be reserved";
        CompletedAt = now;
        return AddResultStep.Completed;
    }

    public void Expire(DateTimeOffset now)
    {
        if (IsDecided)
        {
            return;
        }

        Status = AggregationStatus.TimedOut;
        RejectionReason = $"Timed out after receiving {ReceivedCount} of {LineCount} results";
        CompletedAt = now;
    }

    public void MarkOutcomePublished(DateTimeOffset now) => OutcomePublishedAt = now;
}
