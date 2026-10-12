using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gsm.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehousePartnerContactDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bulstat",
                table: "WarehousePartners",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Egn",
                table: "WarehousePartners",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "WarehousePartners",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "WarehousePartners",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bulstat",
                table: "WarehousePartners");

            migrationBuilder.DropColumn(
                name: "Egn",
                table: "WarehousePartners");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "WarehousePartners");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "WarehousePartners");
        }
    }
}
