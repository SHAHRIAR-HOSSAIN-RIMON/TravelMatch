using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelMatch.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSelectedGuideToOrganizedTrip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SelectedGuideApplicationId",
                table: "OrganizedTrips",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SelectedGuideId",
                table: "OrganizedTrips",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizedTrips_SelectedGuideId",
                table: "OrganizedTrips",
                column: "SelectedGuideId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizedTrips_Users_SelectedGuideId",
                table: "OrganizedTrips",
                column: "SelectedGuideId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrganizedTrips_Users_SelectedGuideId",
                table: "OrganizedTrips");

            migrationBuilder.DropIndex(
                name: "IX_OrganizedTrips_SelectedGuideId",
                table: "OrganizedTrips");

            migrationBuilder.DropColumn(
                name: "SelectedGuideApplicationId",
                table: "OrganizedTrips");

            migrationBuilder.DropColumn(
                name: "SelectedGuideId",
                table: "OrganizedTrips");
        }
    }
}
