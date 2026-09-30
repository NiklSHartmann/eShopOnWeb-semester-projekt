using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderAggregator.Data;

// Used only by "dotnet ef" at design time, so the tool does not have to start the whole worker host.
public sealed class AggregatorDbContextFactory : IDesignTimeDbContextFactory<AggregatorDbContext>
{
    public AggregatorDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AggregatorDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=EShop.OrderAggregator;User Id=sa;Password=@someThingComplicated1234;TrustServerCertificate=true")
            .Options;

        return new AggregatorDbContext(options);
    }
}
