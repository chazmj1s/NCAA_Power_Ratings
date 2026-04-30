using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NCAA_Power_Ratings.Data;
using NCAA_Power_Ratings.Models;

namespace NCAA_Power_Ratings.Utilities
{
    /// <summary>
    /// Calculates and populates matchup-specific historical statistics.
    /// Used to identify high-variance rivalries by comparing actual matchup performance
    /// to expected performance from win-based AvgScoreDeltas.
    /// </summary>
    public class MatchupHistoryCalculator(IDbContextFactory<NCAAContext> contextFactory, ILogger<MatchupHistoryCalculator> logger)
    {
        private class GameData
        {
            public int Team1 { get; set; }
            public int Team2 { get; set; }
            public int Margin { get; set; }
            public int Year { get; set; }
            public int WinnerId { get; set; }
            public int LoserId { get; set; }
        }

        /// <summary>
        /// Calculates matchup history for all team pairings in the database.
        /// Only includes matchups with a minimum number of games for statistical significance.
        /// </summary>
        public async Task<int> CalculateAllMatchupHistories(int minimumGames = 10, CancellationToken cancellationToken = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            logger.LogInformation("Starting matchup history calculation with minimum {MinGames} games", minimumGames);

            // Get all games
            var allGames = await context.Games
                .Select(g => new GameData
                {
                    Team1 = g.WinnerId < g.LoserId ? g.WinnerId : g.LoserId,
                    Team2 = g.WinnerId < g.LoserId ? g.LoserId : g.WinnerId,
                    Margin = g.WPoints - g.LPoints,
                    Year = g.Year,
                    WinnerId = g.WinnerId,
                    LoserId = g.LoserId
                })
                .ToListAsync(cancellationToken);

            // Group by matchup
            var matchupGroups = allGames
                .GroupBy(x => new { x.Team1, x.Team2 })
                .Where(g => g.Count() >= minimumGames)
                .ToList();

            logger.LogInformation("Found {Count} matchups with at least {MinGames} games", matchupGroups.Count, minimumGames);

            // Calculate statistics for each matchup
            var matchupHistories = new List<MatchupHistory>();
            var rivalryMetadata = RivalrySeedData.GetRivalries();

            foreach (var group in matchupGroups)
            {
                var games = group.ToList();
                var gameCount = games.Count;

                // Calculate average margin (absolute value)
                var margins = games.Select(g => Math.Abs(g.Margin)).ToList();
                var avgMargin = margins.Average();

                // Calculate standard deviation
                var variance = margins.Sum(m => Math.Pow(m - avgMargin, 2)) / gameCount;
                var stDev = Math.Sqrt(variance);

                // Calculate upset rate (need to get win records for each game)
                var upsets = await CalculateUpsetRate(context, games, cancellationToken);

                // Check if this is a known rivalry by looking up team names
                var team1Name = await context.Teams
                    .Where(t => t.TeamID == group.Key.Team1)
                    .Select(t => t.TeamName)
                    .FirstOrDefaultAsync(cancellationToken) ?? "";

                var team2Name = await context.Teams
                    .Where(t => t.TeamID == group.Key.Team2)
                    .Select(t => t.TeamName)
                    .FirstOrDefaultAsync(cancellationToken) ?? "";

                var rivalry = rivalryMetadata.FirstOrDefault(r =>
                    (r.Team1Name.Equals(team1Name, StringComparison.OrdinalIgnoreCase) && r.Team2Name.Equals(team2Name, StringComparison.OrdinalIgnoreCase)) ||
                    (r.Team1Name.Equals(team2Name, StringComparison.OrdinalIgnoreCase) && r.Team2Name.Equals(team1Name, StringComparison.OrdinalIgnoreCase)));

                var history = new MatchupHistory
                {
                    Team1Id = group.Key.Team1,
                    Team2Id = group.Key.Team2,
                    GamesPlayed = gameCount,
                    AvgMargin = (decimal)avgMargin,
                    StDevMargin = (decimal)stDev,
                    UpsetRate = (decimal)upsets,
                    FirstPlayed = games.Min(g => g.Year),
                    LastPlayed = games.Max(g => g.Year),
                    RivalryName = rivalry?.RivalryName,
                    RivalryTier = rivalry?.Tier
                };

                matchupHistories.Add(history);
            }

            // Clear existing data and insert new
            await context.Database.ExecuteSqlRawAsync("DELETE FROM MatchupHistory", cancellationToken);
            await context.MatchupHistories.AddRangeAsync(matchupHistories, cancellationToken);
            var saved = await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Saved {Count} matchup histories to database", saved);

            return matchupHistories.Count;
        }

        /// <summary>
        /// Calculates the upset rate for a set of games between two teams.
        /// Upset = team with fewer season wins won the game.
        /// </summary>
        private async Task<double> CalculateUpsetRate(
            NCAAContext context,
            List<GameData> games,
            CancellationToken cancellationToken)
        {
            var upsetCount = 0;
            var totalGames = games.Count;

            foreach (var game in games)
            {
                // Get season records for both teams in that year
                var winnerRecord = await context.TeamRecords
                    .Where(tr => tr.TeamID == game.WinnerId && tr.Year == game.Year)
                    .Select(tr => tr.Wins)
                    .FirstOrDefaultAsync(cancellationToken);

                var loserRecord = await context.TeamRecords
                    .Where(tr => tr.TeamID == game.LoserId && tr.Year == game.Year)
                    .Select(tr => tr.Wins)
                    .FirstOrDefaultAsync(cancellationToken);

                // If loser had more wins, it's an upset
                if (loserRecord > winnerRecord)
                {
                    upsetCount++;
                }
            }

            return totalGames > 0 ? (double)upsetCount / totalGames : 0.0;
        }
    }
}
