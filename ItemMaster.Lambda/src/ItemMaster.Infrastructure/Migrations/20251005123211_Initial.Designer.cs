using System;
using ItemMaster.Infrastructure.Ef;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace ItemMaster.Infrastructure.Migrations
{
    [DbContext(typeof(ItemMasterDbContext))]
    [Migration("20251005123211_Initial")]
    partial class Initial
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("ItemMaster.Infrastructure.Ef.ItemLogEntry", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasColumnName("id");

                    b.Property<string>("RequestId")
                        .IsRequired()
                        .HasColumnType("varchar(255)")
                        .HasColumnName("request_id");

                    b.Property<string>("Sku")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("sku");

                    b.Property<string>("Source")
                        .IsRequired()
                        .HasColumnType("longtext")
                        .HasColumnName("source");

                    b.Property<DateTime>("TimestampUtc")
                        .HasColumnType("datetime(6)")
                        .HasColumnName("timestamp_utc");

                    b.HasKey("Id");

                    b.HasIndex("RequestId");

                    b.ToTable("item_master_logs", (string)null);
                });
        }
    }
}
