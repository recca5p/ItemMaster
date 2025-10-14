using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ItemMaster.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TraceIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                table: "item_master_logs",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TraceId",
                table: "item_master_logs");
        }
    }
}
