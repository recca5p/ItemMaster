using Microsoft.EntityFrameworkCore;

namespace ItemMaster.Infrastructure.Ef;

public sealed class ItemMasterDbContext : DbContext
{
    public ItemMasterDbContext(DbContextOptions<ItemMasterDbContext> options) : base(options) { }

    public DbSet<ItemLogEntry> ItemLogs => Set<ItemLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ItemLogEntry>(e =>
        {
            e.ToTable("item_master_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Sku).HasColumnName("sku").IsRequired();
            e.Property(x => x.Source).HasColumnName("source").IsRequired();
            e.Property(x => x.RequestId).HasColumnName("request_id").IsRequired();
            e.Property(x => x.TimestampUtc).HasColumnName("timestamp_utc").IsRequired();
            e.HasIndex(x => x.RequestId);
        });
    }
}
