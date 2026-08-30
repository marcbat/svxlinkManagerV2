using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SvxlinkManagerV2.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalonType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SalonType",
                table: "Salons",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SalonType",
                table: "Salons");
        }
    }
}
