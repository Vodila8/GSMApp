using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gsm.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseSaleDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "WarehouseSales",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                table: "WarehouseSales",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("UPDATE \"WarehouseSales\" SET \"TotalAmount\" = \"Quantity\" * \"UnitPrice\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "WarehouseSales");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                table: "WarehouseSales");
        }
    }
}
