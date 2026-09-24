using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gsm.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseSaleDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "SaleDate",
                table: "WarehouseSales",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SaleDate",
                table: "WarehouseSales");
        }
    }
}
