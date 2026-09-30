namespace Warehouse;

public sealed class WarehouseOptions
{
    public const string SectionName = "Warehouse";

    // Decides this instance's queue and routing key, e.g. "textiles" => warehouse.textiles
    public string Name { get; set; } = "default";

    // Simulated stock: every item starts with this many units.
    public int InitialStockPerItem { get; set; } = 5;
}
