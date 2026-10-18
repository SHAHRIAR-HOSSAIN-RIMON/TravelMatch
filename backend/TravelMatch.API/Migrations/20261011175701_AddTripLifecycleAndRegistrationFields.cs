using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelMatch.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTripLifecycleAndRegistrationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedPrice",
                table: "TripRequests",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "TripRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "TripRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "TripRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentCompletedAt",
                table: "TripRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "TripRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Registrations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Registrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "Registrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfTravelers",
                table: "Registrations",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Divergent-branch merge: a later merged migration may already have created
            // Status as varchar; only add the legacy integer column when absent.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Registrations' AND column_name = 'Status') THEN
        ALTER TABLE ""Registrations"" ADD COLUMN ""Status"" integer NOT NULL DEFAULT 0;
    END IF;
END $$;");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPrice",
                table: "Registrations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            // Avoid silently assigning misleading values to existing registrations:
            // ensure NumberOfTravelers is at least 1, and backfill TotalPrice from OrganizedTrip PricePerPerson.
            migrationBuilder.Sql(@"
UPDATE ""Registrations""
SET ""NumberOfTravelers"" = 1
WHERE ""NumberOfTravelers"" <= 0;

UPDATE ""Registrations"" r
SET ""TotalPrice"" = ot.""PricePerPerson"" * r.""NumberOfTravelers""
FROM ""OrganizedTrips"" ot
WHERE r.""OrganizedTripId"" = ot.""Id"" AND (r.""TotalPrice"" = 0 OR r.""TotalPrice"" IS NULL);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedPrice",
                table: "TripRequests");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "TripRequests");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "TripRequests");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "TripRequests");

            migrationBuilder.DropColumn(
                name: "PaymentCompletedAt",
                table: "TripRequests");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "TripRequests");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "NumberOfTravelers",
                table: "Registrations");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Registrations' AND column_name = 'Status' AND data_type = 'integer') THEN
        ALTER TABLE ""Registrations"" DROP COLUMN ""Status"";
    END IF;
END $$;");

            migrationBuilder.DropColumn(
                name: "TotalPrice",
                table: "Registrations");
        }
    }
}
