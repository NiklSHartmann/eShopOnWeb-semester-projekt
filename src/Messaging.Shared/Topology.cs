namespace Messaging.Shared;

public static class Topology
{
    // Exchanges
    public const string OrdersExchange = "eshop.orders";            // topic
    public const string OrderLinesExchange = "eshop.order-lines";   // topic
    public const string WarehousesExchange = "eshop.warehouses";    // direct, alternate exchange set by policy
    public const string UnroutedExchange = "eshop.unrouted";        // fanout, used as the alternate exchange
    public const string DeadLetterExchange = "eshop.dlx";           // fanout

    // Routing keys
    public const string OrderPlacedRoutingKey = "order.placed";
    public const string OrderConfirmedRoutingKey = "order.confirmed";
    public const string OrderRejectedRoutingKey = "order.rejected";

    public const string OrderLineRequestedRoutingKey = "order.line.requested";
    public const string OrderLineReservedRoutingKey = "order.line.result.reserved";
    public const string OrderLineUnavailableRoutingKey = "order.line.result.unavailable";

    // Matches both result keys, but NOT order.line.requested.
    public const string OrderLineResultBindingPattern = "order.line.result.*";

    // Deliberately bound by no one: messages sent with this key end up in the alternate exchange.
    public const string UnassignedWarehouseRoutingKey = "warehouse.unassigned";

    public static string WarehouseRoutingKey(string warehouseName) => $"warehouse.{warehouseName}";

    // Queues
    public const string SplitterQueue = "eshop.order-splitter.order-placed";
    public const string RouterQueue = "eshop.order-router.order-lines";
    public const string UnroutedQueue = "eshop.unrouted-messages";
    public const string DeadLetterQueue = "dlq.eshop";
    public const string AggregatorOrderPlacedQueue = "eshop.order-aggregator.order-placed";
    public const string AggregatorResultsQueue = "eshop.order-aggregator.line-results";

    public static string WarehouseQueue(string warehouseName) => $"eshop.warehouse-{warehouseName}.line-requests";
}
