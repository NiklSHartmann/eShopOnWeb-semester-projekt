namespace Messaging.Shared.Contracts;

// Written to the AMQP "type" property so a message can be identified without reading the body.
public static class MessageTypes
{
    public const string OrderPlaced = "order.placed.v1";
    public const string OrderLineReservationRequested = "order.line.reservation-requested.v1";
    public const string OrderLineReservationResult = "order.line.reservation-result.v1";
    public const string OrderConfirmed = "order.confirmed.v1";
    public const string OrderRejected = "order.rejected.v1";
}
