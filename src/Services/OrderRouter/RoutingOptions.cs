namespace OrderRouter;

// The router's routing table: which warehouse handles which catalog type.
public sealed class RoutingOptions
{
    public const string SectionName = "Routing";

    // Key: CatalogTypeId as a string (configuration keys are strings). Value: warehouse name.
    public Dictionary<string, string> WarehouseByCatalogTypeId { get; set; } = new();
}
