using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using NCAA_Rankings.Data;
using NCAA_Rankings.Models;
using System.Text.RegularExpressions;

namespace NCAA_Rankings.Utilities
{
    public class RecordProcessor
    {
        public async Task ProcessSingleRecordAsync(string[] cells, string yearIn, IDbContextFactory<NCAAContext> contextFactory, CancellationToken token)
        {
            var regex = @"\(\d+\)&nbsp;";

            if (cells.Length >= 9)
            {
                var context = contextFactory.CreateDbContext();

                // Extract winnerName and loserName from the appropriate cells
                var winnerName = Regex.Replace(cells[4], regex, "").Trim();
                var loserName = Regex.Replace(cells[7], regex, "").Trim();

                int winnerId = context.Teams.FirstOrDefault(t => t.TeamName == winnerName)?.TeamID ?? -1;
                int loserId = context.Teams.FirstOrDefault(t => t.TeamName == loserName)?.TeamID ?? -1;

                var siteCellText = cells[6].Trim();
                char siteIndicator = siteCellText.Contains('@') ? 'L' : siteCellText.Contains('N') ? 'N' : 'W';

                var gameData = new Game
                {
                    Week = int.TryParse(cells[0].Trim(), out int week) ? week : 0,
                    WinnerId = winnerId,
                    WinnerName = winnerName,
                    WPoints = int.TryParse(cells[5].Trim(), out int wpoints) ? wpoints : 0,
                    Location = siteIndicator,
                    LoserId = loserId,
                    LoserName = loserName,
                    LPoints = int.TryParse(cells[8].Trim(), out int lpoints) ? lpoints : 0,
                    Year = int.TryParse(yearIn, out int year) ? year : 0
                };
                await context.Games.AddAsync(gameData, token);
                await context.SaveChangesAsync(token);
            }
        }
    }
}
