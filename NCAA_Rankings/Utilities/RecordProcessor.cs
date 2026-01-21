using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using NCAA_Rankings.Data;
using NCAA_Rankings.Models;
using System.Text.RegularExpressions;

namespace NCAA_Rankings.Utilities
{
    public class RecordProcessor(IDbContextFactory<NCAAContext> _contextFactory)
    {
        public async Task ProcessSingleRecordAsync(string[] cells, string yearIn, CancellationToken token)
        {
            var regex = @"\(\d+\)\s*";
            var year = int.TryParse(yearIn, out int x) ? x : 0;
            var map = ColumnMap.ForYear(year);
            var xIdx = -1; // Set negative Ids for missing teams

            if (cells.Length >= 9)
            {
                await using var context = _contextFactory.CreateDbContext();

                // Extract winnerName and loserName from the appropriate cells
                var winnerName = Regex.Replace(cells[map.WinnerName], regex, "").Trim();
                var loserName = Regex.Replace(cells[map.LoserName], regex, "").Trim();

                int winnerId = context.Teams.FirstOrDefault(t => winnerName.Contains(t.TeamName.Trim()) ||
                                             (t.Alias != null && winnerName.Contains(t.Alias.Trim())))?.TeamID ?? xIdx;
                int loserId = context.Teams.FirstOrDefault(t => loserName.Contains(t.TeamName.Trim()) ||
                                            (t.Alias != null && loserName.Contains(t.Alias.Trim())))?.TeamID ?? xIdx;

                var siteCellText = cells[map.Location].Trim();
                char siteIndicator = siteCellText.Contains('@') ? 'L' : siteCellText.Contains('N') ? 'N' : 'W';

                var gameData = new Game
                {
                    Id = int.TryParse(yearIn + cells[map.RowId].Trim(), out int id) ? id : 0,
                    Week = int.TryParse(cells[map.Week].Trim(), out int week) ? week : 0,
                    WinnerId = winnerId,
                    WinnerName = winnerName,
                    WPoints = int.TryParse(cells[map.WPoints].Trim(), out int wpoints) ? wpoints : 0,
                    Location = siteIndicator,
                    LoserId = loserId,
                    LoserName = loserName,
                    LPoints = int.TryParse(cells[map.LPoints].Trim(), out int lpoints) ? lpoints : 0,
                    Year = year
                };
                await context.Games.AddAsync(gameData, token);
                await context.SaveChangesAsync(token);
            }
        }
    }
}
