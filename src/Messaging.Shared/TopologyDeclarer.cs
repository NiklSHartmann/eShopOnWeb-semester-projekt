using RabbitMQ.Client;

namespace Messaging.Shared;

// Declarations are idempotent: declaring something that already exists with the SAME settings is a no-op.
// Declaring it with DIFFERENT settings (e.g. classic vs quorum) fails with PRECONDITION_FAILED and closes the channel.
public static class TopologyDeclarer
{
    public static async Task DeclareExchangesAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(Topology.OrdersExchange, ExchangeType.Topic,
            durable: true, autoDelete: false, cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(Topology.OrderLinesExchange, ExchangeType.Topic,
            durable: true, autoDelete: false, cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(Topology.DeadLetterExchange, ExchangeType.Fanout,
            durable: true, autoDelete: false, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(Topology.DeadLetterQueue,
            durable: true, exclusive: false, autoDelete: false,
            arguments: QuorumQueueArguments(), cancellationToken: cancellationToken);

        await channel.QueueBindAsync(Topology.DeadLetterQueue, Topology.DeadLetterExchange,
            routingKey: string.Empty, cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(Topology.WarehousesExchange, ExchangeType.Direct,
            durable: true, autoDelete: false, cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(Topology.UnroutedExchange, ExchangeType.Fanout,
            durable: true, autoDelete: false, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(Topology.UnroutedQueue,
            durable: true, exclusive: false, autoDelete: false,
            arguments: QuorumQueueArguments(), cancellationToken: cancellationToken);

        await channel.QueueBindAsync(Topology.UnroutedQueue, Topology.UnroutedExchange,
            routingKey: string.Empty, cancellationToken: cancellationToken);
    }

    public static async Task DeclareQuorumQueueAsync(
        IChannel channel, string queue, string exchange, string routingKey, CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(queue,
            durable: true, exclusive: false, autoDelete: false,
            arguments: QuorumQueueArguments(), cancellationToken: cancellationToken);

        await channel.QueueBindAsync(queue, exchange, routingKey, cancellationToken: cancellationToken);
    }

    // Queue type is the one setting that must be an argument: it cannot be changed after creation.
    // DLX and delivery-limit come from the policy instead.
    private static Dictionary<string, object?> QuorumQueueArguments() => new()
    {
        ["x-queue-type"] = "quorum"
    };
}
