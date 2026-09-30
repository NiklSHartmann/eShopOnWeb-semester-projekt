using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Infrastructure.Messaging;

// Used when messaging is disabled (tests, Render). Does nothing.
public class NullOrderEventPublisher : IOrderEventPublisher
{
    public Task PublishOrderPlacedAsync(Order order, IReadOnlyCollection<CatalogItem> catalogItems,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
