using Microsoft.EntityFrameworkCore;
using NCAA_Power_Ratings.Data;
using NCAA_Power_Ratings.Models;

namespace NCAA_Power_Ratings.Utilities
{
    public class ScoreDeltaCalculator(IDbContextFactory<NCAAContext> _contextFactory)
    {
        public async Task<List<AvgScoreDeltaStats>> CalculateAvgScoreDeltasAsync(CancellationToken token = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(token);

            // Join Games with TeamRecords for winner and loser, project to CTE-like structure
            var cteData = await context.Games
                .Where(g => g.Year > 0)
                .Join(
                    context.TeamRecords,
                    g => new { TeamId = g.WinnerId, Year = g.Year },
                    tw => new { TeamId = tw.TeamID, Year = (int)tw.Year },
                    (g, tw) => new { Game = g, WinnerRecord = tw }
                )
                .Join(
                    context.TeamRecords,
                    x => new { TeamId = x.Game.LoserId, Year = x.Game.Year },
                    tl => new { TeamId = tl.TeamID, Year = (int)tl.Year },
                    (x, tl) => new
                    {
                        WinnerWins = x.WinnerRecord.Wins,
                        LoserWins = tl.Wins,
                        WinnerPoints = x.Game.WPoints,
                        LoserPoints = x.Game.LPoints
                    }
                )
                .Select(x => new
                {
                    // Normalize so W1 >= W2; adjust Delta accordingly
                    W1 = x.WinnerWins >= x.LoserWins ? x.WinnerWins : x.LoserWins,
                    W2 = x.WinnerWins >= x.LoserWins ? x.LoserWins : x.WinnerWins,
                    Delta = x.WinnerWins >= x.LoserWins
                        ? x.WinnerPoints - x.LoserPoints
                        : x.LoserPoints - x.WinnerPoints
                })
                .ToListAsync(token);

            // Group by (W1, W2) and calculate statistics in memory (STDEVP requires client eval)
            var results = cteData
                .GroupBy(x => new { x.W1, x.W2 })
                .Select(g =>
                {
                    var deltas = g.Select(x => (double)x.Delta).ToList();
                    return new AvgScoreDeltaStats
                    {
                        Team1Wins = g.Key.W1,
                        Team2Wins = g.Key.W2,
                        AvgDelta = Math.Round(deltas.Average(), 0),
                        StdDevP = CalculateStandardDeviationPopulation(deltas),
                        SampleSize = g.Count()
                    };
                })
                .OrderBy(x => x.Team1Wins)
                .ThenBy(x => x.Team2Wins)
                .ToList();

            return results;
        }

        /// <summary>
        /// Calculates population standard deviation (STDEVP equivalent).
        /// </summary>
        private static double CalculateStandardDeviationPopulation(IEnumerable<double> values)
        {
            var valuesList = values.ToList();
            if (valuesList.Count == 0) return 0.0;

            var mean = valuesList.Average();
            var sumOfSquaredDifferences = valuesList.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumOfSquaredDifferences / valuesList.Count);
        }

        /// <summary>
        /// Upserts calculated statistics into the AvgScoreDeltas table.
        /// </summary>
        public async Task UpdateAvgScoreDeltasTableAsync(CancellationToken token = default)
        {
            var stats = await CalculateAvgScoreDeltasAsync(token);

            await using var context = await _contextFactory.CreateDbContextAsync(token);

            var existing = await context.AvgScoreDeltas.ToListAsync(token);

            foreach (var stat in stats)
            {
                var existingRecord = existing.FirstOrDefault(e =>
                    e.Team1Wins == stat.Team1Wins && e.Team2Wins == stat.Team2Wins);

                if (existingRecord != null)
                {
                    existingRecord.AverageScoreDelta = (byte)Math.Round((stat.AvgDelta)); // adjust scale if needed
                    existingRecord.SampleSize = stat.SampleSize;
                }
                else
                {
                    context.AvgScoreDeltas.Add(new AvgScoreDelta
                    {
                        Team1Wins = stat.Team1Wins,
                        Team2Wins = stat.Team2Wins,
                        AverageScoreDelta = (byte)Math.Max(0, Math.Min(255, stat.AvgDelta)),
                        StDevP = (decimal)Math.Round(stat.StdDevP, 8),
                        SampleSize = stat.SampleSize
                    });
                }
            }

            await context.SaveChangesAsync(token);
        }
    }

    /// <summary>   
    /// DTO for average score delta statistics by win counts.
    /// </summary>
    public class AvgScoreDeltaStats
    {
        public byte Team1Wins { get; set; }
        public byte Team2Wins { get; set; }
        public double AvgDelta { get; set; }
        public double StdDevP { get; set; }
        public int SampleSize { get; set; }
    }
}
