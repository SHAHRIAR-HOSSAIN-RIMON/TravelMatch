using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TravelMatch.API.Migrations
{
    /// <inheritdoc />
    public partial class AddEscrowRemediationAndMilestones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_ProposalId_Completed_Unique",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_RegistrationId_Completed_Unique",
                table: "Payments");

            migrationBuilder.AddColumn<string>(
                name: "RefundReason",
                table: "Payments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionNote",
                table: "Disputes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "Disputes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResolvedByUserId",
                table: "Disputes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentMilestones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentId = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HeldAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefundedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentMilestones_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProposalId_Escrow_Unique",
                table: "Payments",
                column: "ProposalId",
                unique: true,
                filter: "\"Status\" IN ('Held','Released','Refunded','PartiallyRefunded','Completed') AND \"ProposalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RegistrationId_Escrow_Unique",
                table: "Payments",
                column: "RegistrationId",
                unique: true,
                filter: "\"Status\" IN ('Held','Released','Refunded','PartiallyRefunded','Completed') AND \"RegistrationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_ResolvedByUserId",
                table: "Disputes",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMilestones_PaymentId",
                table: "PaymentMilestones",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMilestones_PaymentId_Index_Unique",
                table: "PaymentMilestones",
                columns: new[] { "PaymentId", "Index" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Disputes_Users_ResolvedByUserId",
                table: "Disputes",
                column: "ResolvedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Disputes_Users_ResolvedByUserId",
                table: "Disputes");

            migrationBuilder.DropTable(
                name: "PaymentMilestones");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ProposalId_Escrow_Unique",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_RegistrationId_Escrow_Unique",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Disputes_ResolvedByUserId",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "RefundReason",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ResolutionNote",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                table: "Disputes");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProposalId_Completed_Unique",
                table: "Payments",
                column: "ProposalId",
                unique: true,
                filter: "\"Status\" = 'Completed' AND \"ProposalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RegistrationId_Completed_Unique",
                table: "Payments",
                column: "RegistrationId",
                unique: true,
                filter: "\"Status\" = 'Completed' AND \"RegistrationId\" IS NOT NULL");
        }
    }
}
