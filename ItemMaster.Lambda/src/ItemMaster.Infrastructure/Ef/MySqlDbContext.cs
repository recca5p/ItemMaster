using ItemMaster.Shared;
using Microsoft.EntityFrameworkCore;

namespace ItemMaster.Infrastructure.Ef;

public class MySqlDbContext : DbContext
{
    public MySqlDbContext(DbContextOptions<MySqlDbContext> options) : base(options)
    {
    }

    public DbSet<ItemLogRecord> ItemLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ItemLogRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Operation).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Success).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.ItemCount);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.RequestSource).IsRequired().HasConversion<int>();
            entity.Property(e => e.TraceId).HasMaxLength(50);
            entity.ToTable("item_master_logs");
        });

        base.OnModelCreating(modelBuilder);
    }
}