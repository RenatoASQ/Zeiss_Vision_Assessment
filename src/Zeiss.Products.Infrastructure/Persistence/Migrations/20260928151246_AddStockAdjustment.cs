using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeiss.Products.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAdjustment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovements_BalanceChange",
                schema: "dbo",
                table: "StockMovements");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "dbo",
                table: "StockMovements",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(9)",
                oldMaxLength: 9);

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_BalanceChange",
                schema: "dbo",
                table: "StockMovements",
                sql: "([Type] = N'Increment' AND CONVERT(bigint, [StockAfter]) = CONVERT(bigint, [StockBefore]) + [Quantity])\nOR ([Type] = N'Decrement' AND CONVERT(bigint, [StockAfter]) = CONVERT(bigint, [StockBefore]) - [Quantity])\nOR ([Type] = N'Adjustment' AND [Quantity] = ABS(CONVERT(bigint, [StockAfter]) - CONVERT(bigint, [StockBefore])))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovements_BalanceChange",
                schema: "dbo",
                table: "StockMovements");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "dbo",
                table: "StockMovements",
                type: "nvarchar(9)",
                maxLength: 9,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_BalanceChange",
                schema: "dbo",
                table: "StockMovements",
                sql: "([Type] = N'Increment' AND CONVERT(bigint, [StockAfter]) = CONVERT(bigint, [StockBefore]) + [Quantity])\nOR ([Type] = N'Decrement' AND CONVERT(bigint, [StockAfter]) = CONVERT(bigint, [StockBefore]) - [Quantity])");
        }
    }
}
