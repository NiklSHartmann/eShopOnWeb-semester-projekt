using RabbitMQ.Client;

namespace Messaging.Shared;

public static class ChannelPublishExtensions
{
    // Options for a publishing channel with publisher confirms.
    // "Tracking" means the client library waits for the broker's ack/nack for each message on our behalf.
    public static CreateChannelOptions ConfirmedChannelOptions() =>
        new(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);

    public static async Task PublishJsonAsync<T>(
        this IChannel channel,
        string exchange,
        string routingKey,
        T message,
        string messageId,
        string messageType,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var properties = new BasicProperties
        {
            MessageId = messageId,
            CorrelationId = correlationId,
            Type = messageType,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        var body = MessageSerializer.Serialize(message);

        // On a channel created with ConfirmedChannelOptions, this call completes only when the broker has
        // confirmed the message. It throws PublishException if the broker nacks it, or, because mandatory
        // is true, if no queue is bound to receive it (PublishException.IsReturn == true).
        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}
