namespace Messaging.Shared;

// Bound from the "RabbitMq" configuration section, or from environment variables like RabbitMq__HostName.
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    // Only used by the monolith, so messaging can be switched off in tests and on Render.
    public bool Enabled { get; set; }

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "eshop";
    public string Password { get; set; } = "eshop";

    // Shown under "Connections" in the Management UI, so you can see which service owns a connection.
    public string ClientName { get; set; } = "eshop";
}
