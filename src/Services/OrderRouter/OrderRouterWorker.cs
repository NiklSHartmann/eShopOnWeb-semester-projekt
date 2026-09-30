using Messaging.Shared;
using Messaging.Shared.Contracts;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OrderRouter;

// EIP: Content-Based Router. Chooses a warehouse from the message CONTENT (CatalogTypeId), not its routing key.
public sealed class OrderRouterWorker : RabbitMqProcessorService<OrderLineReservationRequested>
{
    private readonly RoutingOptions _routing;

    public OrderRouterWorker(
        RabbitMqConnectionProvider connectionProvider,
        IOptions<RoutingOptions> routing,
        ILogger<OrderRouterWorker> logger)
        : base(connectionProvider, logger)
    {
        _routing = routing.Value;
    }

    protected override string QueueName => Topology.RouterQueue;
    protected override string BindingExchange => Topology.OrderLinesExchange;
    protected override string BindingRoutingKey => Topology.OrderLineRequestedRoutingKey;

    protected override async Task HandleAsync(OrderLineReservationRequested line, IReadOnlyBasicProperties properties,
        CancellationToken cancellationToken)
    {
        var routingKey = _routing.WarehouseByCatalogTypeId.TryGetValue(line.CatalogTypeId.ToString(), out var warehouse)
            ? Topology.WarehouseRoutingKey(warehouse)
            : Topology.UnassignedWarehouseRoutingKey;

        // A router changes WHERE a message goes, not WHAT it is: keep MessageId, Type and CorrelationId,
        // so the message can still be traced and de-duplicated downstream.
        await PublishChannel.PublishJsonAsync(
            Topology.WarehousesExchange,
            routingKey,
            line,
            messageId: properties.MessageId ?? $"order-line-{line.OrderId}-{line.LineNumber}",
            messageType: properties.Type ?? MessageTypes.OrderLineReservationRequested,
            correlationId: properties.CorrelationId ?? line.OrderId.ToString(),
            cancellationToken);

        Logger.LogInformation("Routed order {OrderId} line {LineNumber} (catalog type {CatalogTypeId}) with key {RoutingKey}",
            line.OrderId, line.LineNumber, line.CatalogTypeId, routingKey);
    }
}
