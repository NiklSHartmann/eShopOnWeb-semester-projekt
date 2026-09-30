using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Messaging.Shared;

// A consumer that also publishes: receives on one channel, publishes on another (with confirms).
public abstract class RabbitMqProcessorService<TMessage> : RabbitMqConsumerService<TMessage>
{
    private IChannel? _publishChannel;

    protected RabbitMqProcessorService(RabbitMqConnectionProvider connectionProvider, ILogger logger)
        : base(connectionProvider, logger)
    {
    }

    // Only used from HandleAsync, which runs for one message at a time, so the channel is never shared concurrently.
    protected IChannel PublishChannel =>
        _publishChannel ?? throw new InvalidOperationException("Publish channel is not initialised.");

    protected override async Task OnStartedAsync(IConnection connection, CancellationToken cancellationToken)
    {
        _publishChannel = await connection.CreateChannelAsync(
            ChannelPublishExtensions.ConfirmedChannelOptions(), cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_publishChannel is not null)
        {
            await _publishChannel.DisposeAsync();
        }
    }
}

