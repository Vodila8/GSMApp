using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace gsm.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseItemDeliveryDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DeliveryDate",
                table: "WarehouseItems",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryDate",
                table: "WarehouseItems");
        }
    }
}
