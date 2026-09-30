using Microsoft.EntityFrameworkCore;

namespace OrderAggregator.Data;

public class AggregatorDbContext : DbContext
{
    public AggregatorDbContext(DbContextOptions<AggregatorDbContext> options) : base(options)
    {
    }

    public DbSet<OrderAggregation> Aggregations => Set<OrderAggregation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var aggregation = modelBuilder.Entity<OrderAggregation>();
        aggregation.ToTable("OrderAggregations");
        aggregation.HasKey(a => a.OrderId);
        aggregation.Property(a => a.OrderId).ValueGeneratedNever(); // the id comes from the monolith, not from SQL Server
        aggregation.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        aggregation.Property(a => a.RejectionReason).HasMaxLength(500);
        aggregation.Property(a => a.RowVersion).IsRowVersion();
        aggregation.HasIndex(a => new { a.Status, a.TimeoutAt }); // supports the timeout query

        aggregation.HasMany(a => a.Lines).WithOne().HasForeignKey(l => l.OrderId);
        aggregation.Navigation(a => a.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        var line = modelBuilder.Entity<LineResult>();
        line.ToTable("OrderAggregationLines");
        line.HasKey(l => new { l.OrderId, l.LineNumber });
        line.Property(l => l.Warehouse).HasMaxLength(100);
        line.Property(l => l.Reason).HasMaxLength(500);
    }
}
