using Messaging.Shared;
using Messaging.Shared.Contracts;
using RabbitMQ.Client;

namespace OrderAggregator;

// Starts an aggregation as soon as the order exists, so it can time out even if NO results ever arrive.
public sealed class OrderPlacedListener : RabbitMqConsumerService<OrderPlaced>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public OrderPlacedListener(RabbitMqConnectionProvider connectionProvider, IServiceScopeFactory scopeFactory,
        ILogger<OrderPlacedListener> logger)
        : base(connectionProvider, logger)
    {
        _scopeFactory = scopeFactory;
    }

    protected override string QueueName => Topology.AggregatorOrderPlacedQueue;
    protected override string BindingExchange => Topology.OrdersExchange;
    protected override string BindingRoutingKey => Topology.OrderPlacedRoutingKey;

    protected override async Task HandleAsync(OrderPlaced order, IReadOnlyBasicProperties properties,
        CancellationToken cancellationToken)
    {
        // DbContext is short-lived and not thread-safe: a new scope, and so a new DbContext, per message.
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<OrderAggregationService>();

        var started = await service.StartAsync(order, cancellationToken);
        Logger.LogInformation(started
                ? "Aggregation started for order {OrderId}, expecting {LineCount} results"
                : "Aggregation for order {OrderId} already exists ({LineCount} lines)",
            order.OrderId, order.Lines.Count);
    }
}
