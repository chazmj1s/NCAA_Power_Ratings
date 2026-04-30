using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCAA_Power_Ratings.Migrations
{
    /// <inheritdoc />
    public partial class InitialSQLite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvgScoreDeltas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Team1Wins = table.Column<byte>(type: "tinyint", nullable: false),
                    Team2Wins = table.Column<byte>(type: "tinyint", nullable: false),
                    AverageScoreDelta = table.Column<byte>(type: "decimal(5,4)", nullable: false),
                    StDevP = table.Column<decimal>(type: "decimal(10,8)", nullable: false),
                    SampleSize = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvgScoreDeltas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Game",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Week = table.Column<int>(type: "INTEGER", nullable: false),
                    WinnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    WinnerName = table.Column<string>(type: "varchar(50)", nullable: false),
                    WPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    LoserId = table.Column<int>(type: "INTEGER", nullable: false),
                    LoserName = table.Column<string>(type: "varchar(50)", nullable: false),
                    LPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    Location = table.Column<char>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Game", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Team",
                columns: table => new
                {
                    TeamID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeamName = table.Column<string>(type: "varchar(50)", nullable: false),
                    Alias = table.Column<string>(type: "varchar(50)", nullable: true),
                    Division = table.Column<string>(type: "varchar(20)", nullable: true),
                    Conference = table.Column<string>(type: "varchar(50)", nullable: true),
                    ConferenceAbbr = table.Column<string>(type: "varchar(20)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Team", x => x.TeamID);
                });

            migrationBuilder.CreateTable(
                name: "TeamRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeamID = table.Column<int>(type: "INTEGER", nullable: false),
                    Year = table.Column<short>(type: "smallint", nullable: false),
                    Wins = table.Column<byte>(type: "tinyint", nullable: false),
                    Losses = table.Column<byte>(type: "tinyint", nullable: false),
                    PointsFor = table.Column<int>(type: "INTEGER", nullable: false),
                    PointsAgainst = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamRecords_Team",
                        column: x => x.TeamID,
                        principalTable: "Team",
                        principalColumn: "TeamID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamRecords_TeamID",
                table: "TeamRecords",
                column: "TeamID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvgScoreDeltas");

            migrationBuilder.DropTable(
                name: "Game");

            migrationBuilder.DropTable(
                name: "TeamRecords");

            migrationBuilder.DropTable(
                name: "Team");
        }
    }
}
