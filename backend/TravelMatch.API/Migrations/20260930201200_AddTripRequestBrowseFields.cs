using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelMatch.API.Migrations
{
    public partial class AddTripRequestBrowseFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TripType",
                table: "TripRequests",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.AddColumn<string>(
                name: "TravelPreferences",
                table: "TripRequests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TripType",
                table: "TripRequests");

            migrationBuilder.DropColumn(
                name: "TravelPreferences",
                table: "TripRequests");
        }
    }
}