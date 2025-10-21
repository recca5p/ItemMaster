using ItemMaster.Shared;
using Microsoft.EntityFrameworkCore;

namespace ItemMaster.Infrastructure.Ef;

public class MySqlDbContext : DbContext
{
    public MySqlDbContext(DbContextOptions<MySqlDbContext> options) : base(options)
    {
    }

    public DbSet<ItemMasterSourceLog> ItemMasterSourceLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<ItemMasterSourceLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Sku).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SourceModel).HasColumnType("json");
            entity.Property(e => e.ValidationStatus).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CommonModel).HasColumnType("json");
            entity.Property(e => e.Errors).HasColumnType("text");
            entity.Property(e => e.IsSentToSqs).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasIndex(e => e.Sku).HasDatabaseName("IX_ItemMasterSourceLog_Sku");
            
            entity.ToTable("item_master_source_log");
        });

        base.OnModelCreating(modelBuilder);
    }
}