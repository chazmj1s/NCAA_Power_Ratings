using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCAA_Power_Ratings.Migrations
{
    /// <inheritdoc />
    public partial class ConvertAvgScoreDeltasToWinPercentages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Team1Wins",
                table: "AvgScoreDeltas");

            migrationBuilder.DropColumn(
                name: "Team2Wins",
                table: "AvgScoreDeltas");

            migrationBuilder.AddColumn<decimal>(
                name: "Team1WinPct",
                table: "AvgScoreDeltas",
                type: "decimal(3,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Team2WinPct",
                table: "AvgScoreDeltas",
                type: "decimal(3,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Team1WinPct",
                table: "AvgScoreDeltas");

            migrationBuilder.DropColumn(
                name: "Team2WinPct",
                table: "AvgScoreDeltas");

            migrationBuilder.AddColumn<byte>(
                name: "Team1Wins",
                table: "AvgScoreDeltas",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "Team2Wins",
                table: "AvgScoreDeltas",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);
        }
    }
}
