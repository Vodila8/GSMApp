using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gsm.Migrations
{
    /// <inheritdoc />
    public partial class AddWarrantyDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "WarrantyEndDate",
                table: "ServiceOrders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarrantyMonths",
                table: "ServiceOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WarrantyStartDate",
                table: "ServiceOrders",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WarrantyEndDate",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "WarrantyMonths",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "WarrantyStartDate",
                table: "ServiceOrders");
        }
    }
}
