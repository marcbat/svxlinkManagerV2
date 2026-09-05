using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SvxlinkManagerV2.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LocalHour = table.Column<int>(type: "INTEGER", nullable: false),
                    LocalDayOfWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    SalonId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SalonName = table.Column<string>(type: "TEXT", nullable: true),
                    Callsign = table.Column<string>(type: "TEXT", nullable: true),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    Detail = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalonSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SalonId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SalonName = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Origin = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClosedOnRecovery = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalonSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_Callsign",
                table: "ActivityEvents",
                column: "Callsign");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_OccurredAt_Type",
                table: "ActivityEvents",
                columns: new[] { "OccurredAt", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_SalonSessions_EndedAt",
                table: "SalonSessions",
                column: "EndedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SalonSessions_StartedAt",
                table: "SalonSessions",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityEvents");

            migrationBuilder.DropTable(
                name: "SalonSessions");
        }
    }
}
