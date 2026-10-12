using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace gsm.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessPartnersToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BusinessPartnerConnectionId",
                table: "ServiceOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BusinessPartnerConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerCompanyId = table.Column<string>(type: "text", nullable: false),
                    PartnerCompanyId = table.Column<string>(type: "text", nullable: false),
                    InvitedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    InvitedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerConnections_Companies_OwnerCompanyId",
                        column: x => x.OwnerCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerConnections_Companies_PartnerCompanyId",
                        column: x => x.PartnerCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceOrders_BusinessPartnerConnectionId",
                table: "ServiceOrders",
                column: "BusinessPartnerConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerConnections_OwnerCompanyId_PartnerCompanyId",
                table: "BusinessPartnerConnections",
                columns: new[] { "OwnerCompanyId", "PartnerCompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerConnections_PartnerCompanyId",
                table: "BusinessPartnerConnections",
                column: "PartnerCompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceOrders_BusinessPartnerConnections_BusinessPartnerCon~",
                table: "ServiceOrders",
                column: "BusinessPartnerConnectionId",
                principalTable: "BusinessPartnerConnections",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceOrders_BusinessPartnerConnections_BusinessPartnerCon~",
                table: "ServiceOrders");

            migrationBuilder.DropTable(
                name: "BusinessPartnerConnections");

            migrationBuilder.DropIndex(
                name: "IX_ServiceOrders_BusinessPartnerConnectionId",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "BusinessPartnerConnectionId",
                table: "ServiceOrders");
        }
    }
}
