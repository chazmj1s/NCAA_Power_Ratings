using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCAA_Power_Ratings.Migrations
{
    /// <inheritdoc />
    public partial class AddRivalryMetadataToMatchupHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RivalryName",
                table: "MatchupHistory",
                type: "varchar(100)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RivalryTier",
                table: "MatchupHistory",
                type: "varchar(20)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RivalryName",
                table: "MatchupHistory");

            migrationBuilder.DropColumn(
                name: "RivalryTier",
                table: "MatchupHistory");
        }
    }
}
