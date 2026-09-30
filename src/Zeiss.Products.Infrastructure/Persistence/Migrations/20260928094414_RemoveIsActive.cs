using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeiss.Products.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "dbo",
                table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "dbo",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
