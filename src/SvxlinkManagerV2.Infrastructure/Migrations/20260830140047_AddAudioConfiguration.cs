using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SvxlinkManagerV2.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudioConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CaptureControl = table.Column<string>(type: "TEXT", nullable: false),
                    CaptureLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    PlaybackControl = table.Column<string>(type: "TEXT", nullable: false),
                    PlaybackLevel = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioConfigurations", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudioConfigurations");
        }
    }
}
