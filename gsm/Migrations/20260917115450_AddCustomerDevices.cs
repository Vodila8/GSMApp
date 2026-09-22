using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace gsm.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerDeviceId",
                table: "ServiceOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ModelAndSerialNumber = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerDevices_AspNetUsers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_CustomerDeviceId",
                table: "ServiceOrders",
                column: "CustomerDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDevices_CustomerId",
                table: "CustomerDevices",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceOrders_CustomerDevices_CustomerDeviceId",
                table: "ServiceOrders",
                column: "CustomerDeviceId",
                principalTable: "CustomerDevices",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceOrders_CustomerDevices_CustomerDeviceId",
                table: "ServiceOrders");

            migrationBuilder.DropTable(
                name: "CustomerDevices");

            migrationBuilder.DropIndex(
                name: "IX_ServiceOrders_CustomerDeviceId",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "CustomerDeviceId",
                table: "ServiceOrders");
        }
    }
}
