namespace Warehouse;

// In-memory stock for one warehouse. Not thread-safe on purpose: the worker handles one message at a time.
public sealed class StockLedger
{
    private readonly int _initialStockPerItem;
    private readonly Dictionary<int, int> _availableByItemId = new();
    private readonly Dictionary<(int OrderId, int LineNumber), ReservationOutcome> _outcomes = new();

    public StockLedger(int initialStockPerItem)
    {
        _initialStockPerItem = initialStockPerItem;
    }

    public ReservationOutcome Reserve(int orderId, int lineNumber, int catalogItemId, int units)
    {
        // Idempotent Receiver: a line we have already handled (redelivery, re-split, re-route)
        // gets the SAME answer again, and stock is not reserved twice.
        if (_outcomes.TryGetValue((orderId, lineNumber), out var previous))
        {
            return previous with { IsDuplicate = true };
        }

        var available = _availableByItemId.GetValueOrDefault(catalogItemId, _initialStockPerItem);

        ReservationOutcome outcome;
        if (units <= available)
        {
            _availableByItemId[catalogItemId] = available - units;
            outcome = new ReservationOutcome(Reserved: true, Reason: null, RemainingStock: available - units, IsDuplicate: false);
        }
        else
        {
            outcome = new ReservationOutcome(Reserved: false, Reason: $"Requested {units}, only {available} available",
                RemainingStock: available, IsDuplicate: false);
        }

        _outcomes[(orderId, lineNumber)] = outcome;
        return outcome;
    }
}

public sealed record ReservationOutcome(bool Reserved, string? Reason, int RemainingStock, bool IsDuplicate);
