using Messaging.Shared;
using Messaging.Shared.Contracts;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Warehouse;

// Receives line requests routed to this warehouse, reserves stock, and publishes the result.
public sealed class WarehouseWorker : RabbitMqProcessorService<OrderLineReservationRequested>
{
    private readonly StockLedger _ledger;
    private readonly WarehouseOptions _options;

    public WarehouseWorker(
        RabbitMqConnectionProvider connectionProvider,
        StockLedger ledger,
        IOptions<WarehouseOptions> options,
        ILogger<WarehouseWorker> logger)
        : base(connectionProvider, logger)
    {
        _ledger = ledger;
        _options = options.Value;
    }

    protected override string QueueName => Topology.WarehouseQueue(_options.Name);
    protected override string BindingExchange => Topology.WarehousesExchange;
    protected override string BindingRoutingKey => Topology.WarehouseRoutingKey(_options.Name);
    protected override ushort PrefetchCount => 5;

    protected override async Task HandleAsync(OrderLineReservationRequested line, IReadOnlyBasicProperties properties,
        CancellationToken cancellationToken)
    {
        var outcome = _ledger.Reserve(line.OrderId, line.LineNumber, line.CatalogItemId, line.Units);

        if (outcome.IsDuplicate)
        {
            Logger.LogInformation("Order {OrderId} line {LineNumber} already handled; re-sending the same result",
                line.OrderId, line.LineNumber);
        }

        var result = new OrderLineReservationResult(
            line.OrderId, line.LineNumber, line.LineCount, line.CatalogItemId, line.Units,
            Warehouse: _options.Name, outcome.Reserved, outcome.Reason);

        var routingKey = outcome.Reserved
            ? Topology.OrderLineReservedRoutingKey
            : Topology.OrderLineUnavailableRoutingKey;

        await PublishChannel.PublishJsonAsync(
            Topology.OrderLinesExchange,
            routingKey,
            result,
            messageId: $"order-line-result-{line.OrderId}-{line.LineNumber}",
            messageType: MessageTypes.OrderLineReservationResult,
            correlationId: properties.CorrelationId ?? line.OrderId.ToString(),
            cancellationToken);

        Logger.LogInformation("[{Warehouse}] Order {OrderId} line {LineNumber}: reserved={Reserved}, remaining stock {Remaining}",
            _options.Name, line.OrderId, line.LineNumber, outcome.Reserved, outcome.RemainingStock);
    }
}
