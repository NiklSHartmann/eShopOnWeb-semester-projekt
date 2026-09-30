using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

// Port: the core says "an order was placed" without knowing how that is communicated.
public interface IOrderEventPublisher
{
    Task PublishOrderPlacedAsync(Order order, IReadOnlyCollection<CatalogItem> catalogItems,
        CancellationToken cancellationToken = default);
}
