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
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Registrations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPrice",
                table: "Registrations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
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

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "TotalPrice",
                table: "Registrations");
        }
    }
}
