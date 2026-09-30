using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Messaging.Shared;
using Messaging.Shared.Contracts;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Exceptions;

namespace Microsoft.eShopWeb.Infrastructure.Messaging;

// Adapter: translates the domain Order into the OrderPlaced contract and publishes it.
public class RabbitMqOrderEventPublisher : IOrderEventPublisher
{
    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly ILogger<RabbitMqOrderEventPublisher> _logger;

    public RabbitMqOrderEventPublisher(RabbitMqConnectionProvider connectionProvider,
        ILogger<RabbitMqOrderEventPublisher> logger)
    {
        _connectionProvider = connectionProvider;
        _logger = logger;
    }

    public async Task PublishOrderPlacedAsync(Order order, IReadOnlyCollection<CatalogItem> catalogItems,
        CancellationToken cancellationToken = default)
    {
        // Content Enricher: order lines do not know their catalog type, but the router will need it.
        var typeIdByItemId = catalogItems.ToDictionary(c => c.Id, c => c.CatalogTypeId);

        var message = new OrderPlaced(
            OrderId: order.Id,
            BuyerId: order.BuyerId,
            OrderDate: order.OrderDate,
            Lines: order.OrderItems.Select(item => new OrderPlacedLine(
                CatalogItemId: item.ItemOrdered.CatalogItemId,
                ProductName: item.ItemOrdered.ProductName,
                CatalogTypeId: typeIdByItemId[item.ItemOrdered.CatalogItemId],
                UnitPrice: item.UnitPrice,
                Units: item.Units)).ToList());

        var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);

        // A short-lived channel per publish: web requests run concurrently and must not share a channel.
        await using var channel = await connection.CreateChannelAsync(
            ChannelPublishExtensions.ConfirmedChannelOptions(), cancellationToken);

        await TopologyDeclarer.DeclareExchangesAsync(channel, cancellationToken);

        var orderId = order.Id.ToString();
        try
        {
            await channel.PublishJsonAsync(
                Topology.OrdersExchange,
                Topology.OrderPlacedRoutingKey,
                message,
                messageId: $"order-placed-{orderId}",
                messageType: MessageTypes.OrderPlaced,
                correlationId: orderId,
                cancellationToken);
        }
        catch (PublishException ex)
        {
            _logger.LogError(ex, "OrderPlaced for order {OrderId} was not accepted by the broker (returned: {IsReturn})",
                order.Id, ex.IsReturn);
            throw;
        }

        _logger.LogInformation("Published OrderPlaced for order {OrderId} with {LineCount} lines",
            order.Id, message.Lines.Count);
    }
}
