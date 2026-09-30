using Messaging.Shared;
using Messaging.Shared.Contracts;
using OrderAggregator.Data;
using RabbitMQ.Client;

namespace OrderAggregator;

// EIP: Aggregator (receiving side). Stores each warehouse result; the rules live in OrderAggregation.
public sealed class ReservationResultListener : RabbitMqConsumerService<OrderLineReservationResult>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ReservationResultListener(RabbitMqConnectionProvider connectionProvider, IServiceScopeFactory scopeFactory,
        ILogger<ReservationResultListener> logger)
        : base(connectionProvider, logger)
    {
        _scopeFactory = scopeFactory;
    }

    protected override string QueueName => Topology.AggregatorResultsQueue;
    protected override string BindingExchange => Topology.OrderLinesExchange;
    protected override string BindingRoutingKey => Topology.OrderLineResultBindingPattern;

    protected override async Task HandleAsync(OrderLineReservationResult result, IReadOnlyBasicProperties properties,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<OrderAggregationService>();

        // A DbUpdateConcurrencyException or duplicate-key DbUpdateException thrown here is handled by the base
        // class: nack + requeue, and the retry sees the state the other handler saved.
        var outcome = await service.AddResultAsync(result, cancellationToken);

        switch (outcome.Step)
        {
            case AddResultStep.Waiting:
                Logger.LogInformation("Order {OrderId}: {Received} of {Expected} results received",
                    result.OrderId, outcome.ReceivedCount, outcome.LineCount);
                break;

            case AddResultStep.Completed:
                Logger.LogInformation("Order {OrderId}: all {Expected} results received, decision: {Status}",
                    result.OrderId, outcome.LineCount, outcome.Status);
                break;

            case AddResultStep.Duplicate:
                Logger.LogInformation("Order {OrderId} line {LineNumber}: duplicate result ignored",
                    result.OrderId, result.LineNumber);
                break;

            case AddResultStep.Late:
                Logger.LogWarning(
                    "Order {OrderId} line {LineNumber} arrived after the order was decided ({Status}). Reserved: {Reserved}. " +
                    "Reserved stock for a decided order would need to be released (compensation).",
                    result.OrderId, result.LineNumber, outcome.Status, result.Reserved);
                break;
        }
    }
}
