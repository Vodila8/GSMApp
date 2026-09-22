using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gsm.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIsolationToOrderChildren : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyId",
                table: "ServiceOrderLines",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyId",
                table: "OrderStages",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompanyId",
                table: "OrderAdjustments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE \"ServiceOrderLines\" AS line SET \"CompanyId\" = order_row.\"CompanyId\" FROM \"ServiceOrders\" AS order_row WHERE line.\"ServiceOrderId\" = order_row.\"Id\";");
            migrationBuilder.Sql("UPDATE \"OrderStages\" AS stage SET \"CompanyId\" = order_row.\"CompanyId\" FROM \"ServiceOrders\" AS order_row WHERE stage.\"ServiceOrderId\" = order_row.\"Id\";");
            migrationBuilder.Sql("UPDATE \"OrderAdjustments\" AS adjustment SET \"CompanyId\" = order_row.\"CompanyId\" FROM \"ServiceOrders\" AS order_row WHERE adjustment.\"ServiceOrderId\" = order_row.\"Id\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "ServiceOrderLines");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "OrderStages");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "OrderAdjustments");
        }
    }
}
