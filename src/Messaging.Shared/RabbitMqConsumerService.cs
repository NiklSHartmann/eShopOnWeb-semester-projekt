using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Messaging.Shared;

// Base class for a service that consumes one message type from one quorum queue.
// Subclasses decide which queue and binding to use, and what to do with each message.
public abstract class RabbitMqConsumerService<TMessage> : BackgroundService
{
    private readonly RabbitMqConnectionProvider _connectionProvider;

    protected RabbitMqConsumerService(RabbitMqConnectionProvider connectionProvider, ILogger logger)
    {
        _connectionProvider = connectionProvider;
        Logger = logger;
    }

    protected ILogger Logger { get; }
    protected abstract string QueueName { get; }
    protected abstract string BindingExchange { get; }
    protected abstract string BindingRoutingKey { get; }

    // Max number of unacknowledged messages the broker will push to this consumer at a time.
    protected virtual ushort PrefetchCount => 10;

    // Return normally => the message is acked. Throw => it is nacked and requeued.
    protected abstract Task HandleAsync(TMessage message, IReadOnlyBasicProperties properties, CancellationToken cancellationToken);

    // Optional hook, e.g. for opening a separate publishing channel.
    protected virtual Task OnStartedAsync(IConnection connection, CancellationToken cancellationToken) => Task.CompletedTask;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = await ConnectWithRetryAsync(stoppingToken);

        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        channel.ChannelShutdownAsync += (_, args) =>
        {
            Logger.LogInformation("Consumer channel closed: {ReplyCode} {ReplyText}", args.ReplyCode, args.ReplyText);
            return Task.CompletedTask;
        };

        await TopologyDeclarer.DeclareExchangesAsync(channel, stoppingToken);
        await TopologyDeclarer.DeclareQuorumQueueAsync(channel, QueueName, BindingExchange, BindingRoutingKey, stoppingToken);

        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: PrefetchCount, global: false, cancellationToken: stoppingToken);

        await OnStartedAsync(connection, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, ea) => OnMessageAsync(channel, ea, stoppingToken);

        await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        Logger.LogInformation("Consuming from {Queue} with prefetch {Prefetch}", QueueName, PrefetchCount);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down. Unacked messages are requeued by the broker when the channel closes.
        }
    }

    private async Task OnMessageAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        TMessage message;
        try
        {
            // ea.Body is only valid inside this handler. Deserializing copies the data into new objects.
            message = MessageSerializer.Deserialize<TMessage>(ea.Body);
        }
        catch (JsonException ex)
        {
            // A malformed message will never succeed, so retrying is pointless: reject without requeue => dead-lettered.
            Logger.LogError(ex, "Could not deserialize message {MessageId}; rejecting", ea.BasicProperties.MessageId);
            await channel.BasicRejectAsync(ea.DeliveryTag, requeue: false, cancellationToken);
            return;
        }

        try
        {
            await HandleAsync(message, ea.BasicProperties, cancellationToken);
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Possibly transient: requeue. The quorum queue counts deliveries; after delivery-limit
            // (from the policy) the message is dead-lettered instead of looping forever.
            Logger.LogWarning(ex, "Handling message {MessageId} failed (redelivered: {Redelivered}); requeueing",
                ea.BasicProperties.MessageId, ea.Redelivered);
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }
    }

    private async Task<IConnection> ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                return await _connectionProvider.GetConnectionAsync(cancellationToken);
            }
            catch (BrokerUnreachableException ex)
            {
                // Typical when the service starts before the RabbitMQ container is ready.
                Logger.LogWarning("RabbitMQ not reachable yet, retrying in 5 s: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }
}
