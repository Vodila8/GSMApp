using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gsm.Migrations
{
    /// <inheritdoc />
    public partial class AddRepairAcceptanceDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Accessories",
                table: "ServiceOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceConditionAndNotes",
                table: "ServiceOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DevicePassword",
                table: "ServiceOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProblemOrRepair",
                table: "ServiceOrders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Accessories",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "DeviceConditionAndNotes",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "DevicePassword",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "ProblemOrRepair",
                table: "ServiceOrders");
        }
    }
}
