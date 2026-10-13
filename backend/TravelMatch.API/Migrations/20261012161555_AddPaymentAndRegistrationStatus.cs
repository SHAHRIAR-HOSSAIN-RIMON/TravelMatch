using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TravelMatch.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentAndRegistrationStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Registrations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "PendingPayment");

            // Explicit data backfill for existing rows: ensure any pre-existing rows have valid PendingPayment
            migrationBuilder.Sql("UPDATE \"Registrations\" SET \"Status\" = 'PendingPayment' WHERE \"Status\" IS NULL OR \"Status\" = '' OR \"Status\" = '0';");

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TouristUserId = table.Column<int>(type: "integer", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProposalId = table.Column<int>(type: "integer", nullable: true),
                    RegistrationId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TransactionReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Payments_Registrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "Registrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Payments_Users_TouristUserId",
                        column: x => x.TouristUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProposalId",
                table: "Payments",
                column: "ProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProposalId_Completed_Unique",
                table: "Payments",
                column: "ProposalId",
                unique: true,
                filter: "\"Status\" = 'Completed' AND \"ProposalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RegistrationId",
                table: "Payments",
                column: "RegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RegistrationId_Completed_Unique",
                table: "Payments",
                column: "RegistrationId",
                unique: true,
                filter: "\"Status\" = 'Completed' AND \"RegistrationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TouristUserId",
                table: "Payments",
                column: "TouristUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TransactionReference",
                table: "Payments",
                column: "TransactionReference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Registrations");
        }
    }
}
