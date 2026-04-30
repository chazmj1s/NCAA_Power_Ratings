using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NCAA_Power_Ratings.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAvgScoreDelta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte>(
                name: "AverageScoreDelta",
                table: "AvgScoreDeltas",
                type: "decimal(6,2)",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "decimal(5,4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte>(
                name: "AverageScoreDelta",
                table: "AvgScoreDeltas",
                type: "decimal(5,4)",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "decimal(6,2)");
        }
    }
}
