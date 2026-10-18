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
            // Divergent-branch merge: the pending lifecycle migration adds Status as an
            // integer before this runs. Convert an existing integer column (old enum order
            // 0=Pending, 1=Confirmed, 2=Cancelled), add it when absent, keep it when already varchar.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Registrations' AND column_name = 'Status') THEN
        ALTER TABLE ""Registrations"" ADD COLUMN ""Status"" character varying(30) NOT NULL DEFAULT 'PendingPayment';
    ELSIF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Registrations' AND column_name = 'Status' AND data_type = 'integer') THEN
        ALTER TABLE ""Registrations""
            ALTER COLUMN ""Status"" TYPE character varying(30)
            USING CASE ""Status""
                      WHEN 0 THEN 'PendingPayment'
                      WHEN 1 THEN 'Confirmed'
                      WHEN 2 THEN 'Cancelled'
                      ELSE 'PendingPayment'
                  END;
        ALTER TABLE ""Registrations"" ALTER COLUMN ""Status"" SET DEFAULT 'PendingPayment';
    ELSE
        -- Ensure any pre-existing varchar column sets proper default and sanitizes legacy values
        ALTER TABLE ""Registrations"" ALTER COLUMN ""Status"" SET DEFAULT 'PendingPayment';
        UPDATE ""Registrations"" SET ""Status"" = 'PendingPayment' WHERE ""Status"" IS NULL OR ""Status"" = '' OR ""Status"" = '0' OR ""Status"" = 'Pending';
    END IF;
END $$;");

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

            // Ensure rollback does not accidentally delete a pre-existing string status column or its data:
            // Only convert Status back to integer if the preceding lifecycle migration (20261011175701) is recorded
            // in history. If lifecycle migration is absent, preserve the string status column and its data.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM ""__EFMigrationsHistory""
        WHERE ""MigrationId"" = '20261011175701_AddTripLifecycleAndRegistrationFields') THEN
        IF EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_name = 'Registrations' AND column_name = 'Status' AND data_type LIKE '%char%') THEN
            ALTER TABLE ""Registrations""
                ALTER COLUMN ""Status"" TYPE integer
                USING CASE ""Status""
                          WHEN 'PendingPayment' THEN 0
                          WHEN 'Pending' THEN 0
                          WHEN 'Confirmed' THEN 1
                          WHEN 'Cancelled' THEN 2
                          ELSE 0
                      END;
            ALTER TABLE ""Registrations"" ALTER COLUMN ""Status"" SET DEFAULT 0;
        END IF;
    END IF;
END $$;");
        }
    }
}
