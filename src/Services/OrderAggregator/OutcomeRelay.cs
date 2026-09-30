using Messaging.Shared;
using Messaging.Shared.Contracts;
using Microsoft.Extensions.Options;
using OrderAggregator.Data;
using RabbitMQ.Client;

namespace OrderAggregator;

// Two jobs on a timer:
//  1. Timeout: mark overdue Pending aggregations as TimedOut.
//  2. Outbox relay: publish every decided outcome that has not been published yet, then mark it.
// Because the decision is stored before it is published, a failed publish is simply retried on the next tick.
public sealed class OutcomeRelay : BackgroundService
{
    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AggregatorOptions _options;
    private readonly ILogger<OutcomeRelay> _logger;

    public OutcomeRelay(RabbitMqConnectionProvider connectionProvider, IServiceScopeFactory scopeFactory,
        IOptions<AggregatorOptions> options, ILogger<OutcomeRelay> logger)
    {
        _connectionProvider = connectionProvider;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.RelayInterval);
        IChannel? channel = null;

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ExpireOverdueAsync(stoppingToken);
                    channel = await EnsureChannelAsync(channel, stoppingToken);
                    await PublishPendingOutcomesAsync(channel, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Database or broker trouble, or an unroutable outcome. Nothing is lost: the outcome stays
                    // unpublished in the database and is tried again on the next tick.
                    _logger.LogWarning(ex, "Outcome relay tick failed; retrying on the next tick");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down.
        }
        finally
        {
            if (channel is not null)
            {
                await channel.DisposeAsync();
            }
        }
    }

    private async Task ExpireOverdueAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<OrderAggregationService>();

        foreach (var orderId in await service.ExpireOverdueAsync(cancellationToken))
        {
            _logger.LogWarning("Order {OrderId} timed out", orderId);
        }
    }

    private async Task PublishPendingOutcomesAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<OrderAggregationService>();

        foreach (var aggregation in await service.GetUnpublishedOutcomesAsync(cancellationToken))
        {
            await PublishOutcomeAsync(channel, aggregation, cancellationToken);

            // If the process dies between these two lines, the outcome is published again on restart.
            // Same MessageId both times, so consumers can recognise the duplicate (at-least-once).
            await service.MarkPublishedAsync(aggregation, cancellationToken);

            _logger.LogInformation("Published {Status} for order {OrderId}", aggregation.Status, aggregation.OrderId);
        }
    }

    private static async Task PublishOutcomeAsync(IChannel channel, OrderAggregation aggregation,
        CancellationToken cancellationToken)
    {
        var lines = aggregation.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => new OrderLineOutcome(l.LineNumber, l.CatalogItemId, l.Units, l.Warehouse, l.Reserved, l.Reason))
            .ToList();

        var messageId = $"order-outcome-{aggregation.OrderId}";
        var correlationId = aggregation.OrderId.ToString();

        if (aggregation.Status == AggregationStatus.Confirmed)
        {
            await channel.PublishJsonAsync(
                Topology.OrdersExchange,
                Topology.OrderConfirmedRoutingKey,
                new OrderConfirmed(aggregation.OrderId, lines),
                messageId: messageId,
                messageType: MessageTypes.OrderConfirmed,
                correlationId: correlationId,
                cancellationToken);
        }
        else
        {
            await channel.PublishJsonAsync(
                Topology.OrdersExchange,
                Topology.OrderRejectedRoutingKey,
                new OrderRejected(
                    aggregation.OrderId,
                    aggregation.RejectionReason ?? "Rejected",
                    TimedOut: aggregation.Status == AggregationStatus.TimedOut,
                    lines),
                messageId: messageId,
                messageType: MessageTypes.OrderRejected,
                correlationId: correlationId,
                cancellationToken);
        }
    }

    private async Task<IChannel> EnsureChannelAsync(IChannel? channel, CancellationToken cancellationToken)
    {
        if (channel is { IsOpen: true })
        {
            return channel;
        }

        if (channel is not null)
        {
            await channel.DisposeAsync();
        }

        var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
        var newChannel = await connection.CreateChannelAsync(
            ChannelPublishExtensions.ConfirmedChannelOptions(), cancellationToken);

        await TopologyDeclarer.DeclareExchangesAsync(newChannel, cancellationToken);
        return newChannel;
    }
}
