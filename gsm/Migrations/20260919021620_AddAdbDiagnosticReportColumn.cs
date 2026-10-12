using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gsm.Migrations
{
    /// <inheritdoc />
    public partial class AddAdbDiagnosticReportColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdbDiagnosticReport",
                table: "ServiceOrders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdbDiagnosticReport",
                table: "ServiceOrders");
        }
    }
}
