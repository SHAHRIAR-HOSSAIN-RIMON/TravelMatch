using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelMatch.API.Migrations
{
    /// <inheritdoc />
    public partial class AddProfilePhotoForTouristAndOrganizer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "TouristProfiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "OrganizerProfiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "TouristProfiles");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "OrganizerProfiles");
        }
    }
}
