using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace gsm.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehousePartners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PartnerId",
                table: "WarehouseItems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WarehousePartners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehousePartners", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseItems_PartnerId",
                table: "WarehouseItems",
                column: "PartnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseItems_WarehousePartners_PartnerId",
                table: "WarehouseItems",
                column: "PartnerId",
                principalTable: "WarehousePartners",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseItems_WarehousePartners_PartnerId",
                table: "WarehouseItems");

            migrationBuilder.DropTable(
                name: "WarehousePartners");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseItems_PartnerId",
                table: "WarehouseItems");

            migrationBuilder.DropColumn(
                name: "PartnerId",
                table: "WarehouseItems");
        }
    }
}
