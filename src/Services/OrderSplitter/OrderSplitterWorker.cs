using Messaging.Shared;
using Messaging.Shared.Contracts;
using RabbitMQ.Client;

namespace OrderSplitter;

// EIP: Splitter. Consumes one OrderPlaced and publishes one OrderLineReservationRequested per order line.
// The publish channel is created and disposed by RabbitMqProcessorService.
public sealed class OrderSplitterWorker : RabbitMqProcessorService<OrderPlaced>
{
    public OrderSplitterWorker(RabbitMqConnectionProvider connectionProvider, ILogger<OrderSplitterWorker> logger)
        : base(connectionProvider, logger)
    {
    }

    protected override string QueueName => Topology.SplitterQueue;
    protected override string BindingExchange => Topology.OrdersExchange;
    protected override string BindingRoutingKey => Topology.OrderPlacedRoutingKey;

    protected override async Task HandleAsync(OrderPlaced order, IReadOnlyBasicProperties properties,
        CancellationToken cancellationToken)
    {
        var lineCount = order.Lines.Count;
        if (lineCount == 0)
        {
            // Nothing to split. The monolith guards against empty baskets, but a consumer should never trust that.
            Logger.LogWarning("Order {OrderId} has no lines; nothing to split", order.OrderId);
            return;
        }

        var correlationId = order.OrderId.ToString();

        for (var index = 0; index < lineCount; index++)
        {
            var line = order.Lines[index];
            var lineNumber = index + 1;

            var lineMessage = new OrderLineReservationRequested(
                OrderId: order.OrderId,
                LineNumber: lineNumber,
                LineCount: lineCount,
                CatalogItemId: line.CatalogItemId,
                ProductName: line.ProductName,
                CatalogTypeId: line.CatalogTypeId,
                Units: line.Units);

            // Deterministic MessageId: if this OrderPlaced is redelivered and split again,
            // the duplicates get the same ids, so downstream consumers can detect them.
            await PublishChannel.PublishJsonAsync(
                Topology.OrderLinesExchange,
                Topology.OrderLineRequestedRoutingKey,
                lineMessage,
                messageId: $"order-line-{order.OrderId}-{lineNumber}",
                messageType: MessageTypes.OrderLineReservationRequested,
                correlationId: correlationId,
                cancellationToken);
        }

        Logger.LogInformation("Split order {OrderId} into {LineCount} line messages", order.OrderId, lineCount);
    }
}
