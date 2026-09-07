using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SvxlinkManagerV2.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTalkGroupToActivityEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TalkGroup",
                table: "ActivityEvents",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TalkGroup",
                table: "ActivityEvents");
        }
    }
}
