using Messaging.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderAggregator.Data;

namespace OrderAggregator;

public sealed record AddResultOutcome(AddResultStep Step, int ReceivedCount, int LineCount, AggregationStatus Status);

// Loads and saves aggregations. Scoped: one instance (and one DbContext) per message or per relay tick.
public sealed class OrderAggregationService
{
    private readonly AggregatorDbContext _db;
    private readonly AggregatorOptions _options;
    private readonly TimeProvider _time;

    public OrderAggregationService(AggregatorDbContext db, IOptions<AggregatorOptions> options, TimeProvider time)
    {
        _db = db;
        _options = options.Value;
        _time = time;
    }

    private DateTimeOffset Now => _time.GetUtcNow();

    public async Task<bool> StartAsync(OrderPlaced order, CancellationToken cancellationToken)
    {
        // Already started: either a result overtook OrderPlaced, or OrderPlaced was redelivered.
        if (await _db.Aggregations.AnyAsync(a => a.OrderId == order.OrderId, cancellationToken))
        {
            return false;
        }

        _db.Aggregations.Add(new OrderAggregation(order.OrderId, order.Lines.Count, Now, _options.Timeout));
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AddResultOutcome> AddResultAsync(OrderLineReservationResult result, CancellationToken cancellationToken)
    {
        var aggregation = await _db.Aggregations
            .Include(a => a.Lines)
            .SingleOrDefaultAsync(a => a.OrderId == result.OrderId, cancellationToken);

        if (aggregation is null)
        {
            // OrderPlaced and results arrive on different queues, so there is no ordering between them.
            aggregation = new OrderAggregation(result.OrderId, result.LineCount, Now, _options.Timeout);
            _db.Aggregations.Add(aggregation);
        }

        var step = aggregation.AddResult(result.LineNumber, result.CatalogItemId, result.Units,
            result.Warehouse, result.Reserved, result.Reason, Now);

        if (step != AddResultStep.Duplicate)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new AddResultOutcome(step, aggregation.ReceivedCount, aggregation.LineCount, aggregation.Status);
    }

    public async Task<IReadOnlyList<int>> ExpireOverdueAsync(CancellationToken cancellationToken)
    {
        var now = Now;
        var overdue = await _db.Aggregations
            .Where(a => a.Status == AggregationStatus.Pending && a.TimeoutAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var aggregation in overdue)
        {
            aggregation.Expire(now);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return overdue.Select(a => a.OrderId).ToList();
    }

    public Task<List<OrderAggregation>> GetUnpublishedOutcomesAsync(CancellationToken cancellationToken) =>
        _db.Aggregations
            .Include(a => a.Lines)
            .Where(a => a.Status != AggregationStatus.Pending && a.OutcomePublishedAt == null)
            .OrderBy(a => a.CompletedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

    public async Task MarkPublishedAsync(OrderAggregation aggregation, CancellationToken cancellationToken)
    {
        aggregation.MarkOutcomePublished(Now);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
