using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SvxlinkManagerV2.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeneralConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartReflectorOnStartup = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartDefaultSalonOnStartup = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultRxFrequency = table.Column<decimal>(type: "TEXT", nullable: false),
                    DefaultTxFrequency = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reflectors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Config = table.Column<string>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reflectors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SA818",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Volume = table.Column<int>(type: "INTEGER", nullable: false),
                    Squelch = table.Column<int>(type: "INTEGER", nullable: false),
                    Bandwidth = table.Column<int>(type: "INTEGER", nullable: false),
                    PreEmph = table.Column<bool>(type: "INTEGER", nullable: false),
                    HighPass = table.Column<bool>(type: "INTEGER", nullable: false),
                    LowPass = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SA818", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Salons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DtmfCode = table.Column<int>(type: "INTEGER", nullable: true),
                    Configuration = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Salons", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeneralConfigurations");

            migrationBuilder.DropTable(
                name: "Reflectors");

            migrationBuilder.DropTable(
                name: "SA818");

            migrationBuilder.DropTable(
                name: "Salons");
        }
    }
}
