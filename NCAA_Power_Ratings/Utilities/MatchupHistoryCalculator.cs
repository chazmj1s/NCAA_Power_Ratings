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
        /// Calculates matchup history for all 50 curated Epic, National, State, and MEH tier rivalries.
        /// All rivalries in the seed data have sufficient game history (50+ games).
        /// </summary>
        public async Task<int> CalculateAllMatchupHistories(CancellationToken cancellationToken = default)
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            logger.LogInformation("Starting matchup history calculation for all rivalry tiers");

            // Get all rivalry metadata (Epic, National, State, and MEH tiers)
            var rivalryMetadata = RivalrySeedData.GetRivalries();

            logger.LogInformation("Found {Count} rivalries to process", rivalryMetadata.Count);

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

            // Get team name to ID mapping (include alias for matching)
            var teamMapping = await context.Teams
                .Select(t => new { t.TeamID, t.TeamName, t.Alias })
                .ToListAsync(cancellationToken);

            var matchupHistories = new List<MatchupHistory>();

            // Process each rivalry
            foreach (var rivalry in rivalryMetadata)
            {
                // Find team IDs for this rivalry (check both TeamName and Alias)
                var team1Id = teamMapping.FirstOrDefault(t => 
                    t.TeamName.Equals(rivalry.Team1Name, StringComparison.OrdinalIgnoreCase) ||
                    (t.Alias != null && t.Alias.Equals(rivalry.Team1Name, StringComparison.OrdinalIgnoreCase)))?.TeamID;

                var team2Id = teamMapping.FirstOrDefault(t => 
                    t.TeamName.Equals(rivalry.Team2Name, StringComparison.OrdinalIgnoreCase) ||
                    (t.Alias != null && t.Alias.Equals(rivalry.Team2Name, StringComparison.OrdinalIgnoreCase)))?.TeamID;

                if (team1Id == null || team2Id == null)
                {
                    logger.LogWarning("Could not find team IDs for rivalry {RivalryName} ({Team1} vs {Team2})",
                        rivalry.RivalryName, rivalry.Team1Name, rivalry.Team2Name);
                    continue;
                }

                // Normalize team IDs so lower ID is always Team1
                var normalizedTeam1 = Math.Min(team1Id.Value, team2Id.Value);
                var normalizedTeam2 = Math.Max(team1Id.Value, team2Id.Value);

                // Get games for this matchup
                var games = allGames
                    .Where(g => g.Team1 == normalizedTeam1 && g.Team2 == normalizedTeam2)
                    .ToList();

                if (games.Count == 0)
                {
                    logger.LogWarning("No games found for rivalry {RivalryName} ({Team1} vs {Team2})",
                        rivalry.RivalryName, rivalry.Team1Name, rivalry.Team2Name);
                    continue;
                }

                var gameCount = games.Count;

                // Calculate average margin (absolute value)
                var margins = games.Select(g => Math.Abs(g.Margin)).ToList();
                var avgMargin = margins.Average();

                // Calculate standard deviation
                var variance = margins.Sum(m => Math.Pow(m - avgMargin, 2)) / gameCount;
                var stDev = Math.Sqrt(variance);

                // Calculate upset rate (need to get win records for each game)
                var upsets = await CalculateUpsetRate(context, games, cancellationToken);

                var history = new MatchupHistory
                {
                    Team1Id = normalizedTeam1,
                    Team2Id = normalizedTeam2,
                    GamesPlayed = gameCount,
                    AvgMargin = (decimal)avgMargin,
                    StDevMargin = (decimal)stDev,
                    UpsetRate = (decimal)upsets,
                    FirstPlayed = games.Min(g => g.Year),
                    LastPlayed = games.Max(g => g.Year),
                    RivalryName = rivalry.RivalryName,
                    RivalryTier = rivalry.Tier
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
