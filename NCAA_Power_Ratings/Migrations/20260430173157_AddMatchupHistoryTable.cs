using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCAA_Power_Ratings.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchupHistoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchupHistory",
                columns: table => new
                {
                    Team1Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Team2Id = table.Column<int>(type: "INTEGER", nullable: false),
                    GamesPlayed = table.Column<int>(type: "INTEGER", nullable: false),
                    AvgMargin = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    StDevMargin = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    UpsetRate = table.Column<decimal>(type: "decimal(4,3)", nullable: false),
                    LastPlayed = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstPlayed = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchupHistory", x => new { x.Team1Id, x.Team2Id });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchupHistory");
        }
    }
}
