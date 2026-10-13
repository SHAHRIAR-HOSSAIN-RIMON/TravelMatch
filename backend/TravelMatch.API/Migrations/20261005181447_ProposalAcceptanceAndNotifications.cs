using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TravelMatch.API.Migrations
{
    public partial class ProposalAcceptanceAndNotifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MatchedGuideId",
                table: "TripRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "GuideProfiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Proposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TripRequestId = table.Column<int>(type: "integer", nullable: false),
                    GuideId = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Availability = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Inclusions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Exclusions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    MatchingScore = table.Column<decimal>(type: "numeric", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Proposals_TripRequests_TripRequestId",
                        column: x => x.TripRequestId,
                        principalTable: "TripRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Proposals_Users_GuideId",
                        column: x => x.GuideId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecipientUserId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNotifications_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItineraryDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProposalId = table.Column<int>(type: "integer", nullable: false),
                    DayNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Activities = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Accommodation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Meals = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItineraryDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItineraryDays_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TripRequests_MatchedGuideId",
                table: "TripRequests",
                column: "MatchedGuideId");

            migrationBuilder.CreateIndex(
                name: "IX_ItineraryDays_ProposalId_DayNumber",
                table: "ItineraryDays",
                columns: new[] { "ProposalId", "DayNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_GuideId",
                table: "Proposals",
                column: "GuideId");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_TripRequestId_GuideId",
                table: "Proposals",
                columns: new[] { "TripRequestId", "GuideId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_RecipientUserId_CreatedAt",
                table: "UserNotifications",
                columns: new[] { "RecipientUserId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_TripRequests_Users_MatchedGuideId",
                table: "TripRequests",
                column: "MatchedGuideId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripRequests_Users_MatchedGuideId",
                table: "TripRequests");

            migrationBuilder.DropTable(
                name: "ItineraryDays");

            migrationBuilder.DropTable(
                name: "UserNotifications");

            migrationBuilder.DropTable(
                name: "Proposals");

            migrationBuilder.DropIndex(
                name: "IX_TripRequests_MatchedGuideId",
                table: "TripRequests");

            migrationBuilder.DropColumn(
                name: "MatchedGuideId",
                table: "TripRequests");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "GuideProfiles");
        }
    }
}
