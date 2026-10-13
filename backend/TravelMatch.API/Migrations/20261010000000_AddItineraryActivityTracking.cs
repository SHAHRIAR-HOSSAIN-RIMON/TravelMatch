using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TravelMatch.API.Migrations
{
    public partial class AddItineraryActivityTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItineraryActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItineraryDayId = table.Column<int>(type: "integer", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItineraryActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItineraryActivities_ItineraryDays_ItineraryDayId",
                        column: x => x.ItineraryDayId,
                        principalTable: "ItineraryDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ItineraryDays",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Schedule",
                table: "ItineraryDays",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ItineraryDays",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "Price",
                table: "Proposals",
                newName: "ProposedPrice");

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "Proposals",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "GuideApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuideId = table.Column<int>(type: "integer", nullable: false),
                    TripId = table.Column<int>(type: "integer", nullable: false),
                    ProposedPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuideApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuideApplications_OrganizedTrips_TripId",
                        column: x => x.TripId,
                        principalTable: "OrganizedTrips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuideApplications_Users_GuideId",
                        column: x => x.GuideId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItineraryActivities_ItineraryDayId_OrderIndex",
                table: "ItineraryActivities",
                columns: new[] { "ItineraryDayId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuideApplications_GuideId",
                table: "GuideApplications",
                column: "GuideId");

            migrationBuilder.CreateIndex(
                name: "IX_GuideApplications_TripId",
                table: "GuideApplications",
                column: "TripId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItineraryActivities");

            migrationBuilder.DropTable(
                name: "GuideApplications");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ItineraryDays");

            migrationBuilder.DropColumn(
                name: "Schedule",
                table: "ItineraryDays");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ItineraryDays");

            migrationBuilder.RenameColumn(
                name: "ProposedPrice",
                table: "Proposals",
                newName: "Price");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "Proposals");
        }
    }
}