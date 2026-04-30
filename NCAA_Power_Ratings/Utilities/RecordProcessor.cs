using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using NCAA_Power_Ratings.Data;
using NCAA_Power_Ratings.Models;
using NCAA_Power_Ratings.Extensions;

namespace NCAA_Power_Ratings.Utilities
{
    public class RecordProcessor(IDbContextFactory<NCAAContext> _contextFactory)
    {
        public async Task ProcessSingleRecordAsync(string[] cells, string yearIn, CancellationToken token)
        {
            var year = int.TryParse(yearIn, out int x) ? x : 0;

            if (cells.Length >= 9)
            {
                await using var context = _contextFactory.CreateDbContext();

                // Load teams into memory to perform string.Split in C# instead of SQL
                var teams = await context.Teams.ToListAsync(token);

                // Use extension method to convert string array to Game
                var gameData = cells.ToGame(yearIn, teams);

                await context.Games.AddAsync(gameData, token);
                await context.SaveChangesAsync(token);
            }
        }
    }
}
