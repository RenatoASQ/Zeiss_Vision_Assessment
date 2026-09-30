using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeiss.Products.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateSequence<int>(
                name: "ProductIds",
                schema: "dbo",
                startValue: 100000L,
                minValue: 100000L,
                maxValue: 999999L);

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false, defaultValueSql: "NEXT VALUE FOR [dbo].[ProductIds]"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_CI_AI"),
                    Stock = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Products_Id_Range", "[Id] BETWEEN 100000 AND 999999");
                    table.CheckConstraint("CK_Products_Stock_NonNegative", "[Stock] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    StockBefore = table.Column<int>(type: "int", nullable: false),
                    StockAfter = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.CheckConstraint("CK_StockMovements_BalanceChange", "([Type] = N'Increment' AND CONVERT(bigint, [StockAfter]) = CONVERT(bigint, [StockBefore]) + [Quantity])\nOR ([Type] = N'Decrement' AND CONVERT(bigint, [StockAfter]) = CONVERT(bigint, [StockBefore]) - [Quantity])");
                    table.CheckConstraint("CK_StockMovements_Balances_NonNegative", "[StockBefore] >= 0 AND [StockAfter] >= 0");
                    table.CheckConstraint("CK_StockMovements_Quantity_Positive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_StockMovements_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name",
                schema: "dbo",
                table: "Products",
                column: "Name",
                unique: true,
                filter: "[DeletedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductId",
                schema: "dbo",
                table: "StockMovements",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockMovements",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "dbo");

            migrationBuilder.DropSequence(
                name: "ProductIds",
                schema: "dbo");
        }
    }
}
