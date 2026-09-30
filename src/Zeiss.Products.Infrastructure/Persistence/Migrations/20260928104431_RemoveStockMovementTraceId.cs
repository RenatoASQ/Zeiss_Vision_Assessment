using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeiss.Products.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStockMovementTraceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TraceId",
                schema: "dbo",
                table: "StockMovements");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                schema: "dbo",
                table: "StockMovements",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
