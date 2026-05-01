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

            // Join Games with TeamRecords for winner and loser, calculate win percentages
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
                        WinnerLosses = x.WinnerRecord.Losses,
                        LoserWins = tl.Wins,
                        LoserLosses = tl.Losses,
                        WinnerPoints = x.Game.WPoints,
                        LoserPoints = x.Game.LPoints
                    }
                )
                .Select(x => new
                {
                    WinnerWinPct = (x.WinnerWins + x.WinnerLosses) > 0 
                        ? (decimal)x.WinnerWins / (x.WinnerWins + x.WinnerLosses) 
                        : 0m,
                    LoserWinPct = (x.LoserWins + x.LoserLosses) > 0 
                        ? (decimal)x.LoserWins / (x.LoserWins + x.LoserLosses) 
                        : 0m,
                    WinnerPoints = x.WinnerPoints,
                    LoserPoints = x.LoserPoints
                })
                .ToListAsync(token);

            // Round to 0.05 increments (5% buckets) and normalize so Team1WinPct >= Team2WinPct
            var normalizedData = cteData
                .Select(x => new
                {
                    WinPct1 = Math.Round(x.WinnerWinPct * 20m, MidpointRounding.AwayFromZero) / 20m,
                    WinPct2 = Math.Round(x.LoserWinPct * 20m, MidpointRounding.AwayFromZero) / 20m,
                    WinnerPoints = x.WinnerPoints,
                    LoserPoints = x.LoserPoints
                })
                .Select(x => new
                {
                    Team1WinPct = x.WinPct1 >= x.WinPct2 ? x.WinPct1 : x.WinPct2,
                    Team2WinPct = x.WinPct1 >= x.WinPct2 ? x.WinPct2 : x.WinPct1,
                    Delta = x.WinPct1 >= x.WinPct2
                        ? x.WinnerPoints - x.LoserPoints
                        : x.LoserPoints - x.WinnerPoints
                })
                .ToList();

            // Group by (Team1WinPct, Team2WinPct) and calculate statistics
            var results = normalizedData
                .GroupBy(x => new { x.Team1WinPct, x.Team2WinPct })
                .Select(g =>
                {
                    var deltas = g.Select(x => (double)x.Delta).ToList();
                    return new AvgScoreDeltaStats
                    {
                        Team1WinPct = g.Key.Team1WinPct,
                        Team2WinPct = g.Key.Team2WinPct,
                        AvgDelta = Math.Round(deltas.Average(), 2),
                        StdDevP = CalculateStandardDeviationPopulation(deltas),
                        SampleSize = g.Count()
                    };
                })
                .OrderByDescending(x => x.Team1WinPct)
                .ThenByDescending(x => x.Team2WinPct)
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
                    e.Team1WinPct == stat.Team1WinPct && e.Team2WinPct == stat.Team2WinPct);

                if (existingRecord != null)
                {
                    existingRecord.AverageScoreDelta = (decimal)Math.Round(stat.AvgDelta, 2);
                    existingRecord.StDevP = (decimal)Math.Round(stat.StdDevP, 8);
                    existingRecord.SampleSize = stat.SampleSize;
                }
                else
                {
                    context.AvgScoreDeltas.Add(new AvgScoreDelta
                    {
                        Team1WinPct = stat.Team1WinPct,
                        Team2WinPct = stat.Team2WinPct,
                        AverageScoreDelta = (decimal)Math.Round(stat.AvgDelta, 2),
                        StDevP = (decimal)Math.Round(stat.StdDevP, 8),
                        SampleSize = stat.SampleSize
                    });
                }
            }

            await context.SaveChangesAsync(token);
        }
    }

    /// <summary>   
    /// DTO for average score delta statistics by win percentages.
    /// </summary>
    public class AvgScoreDeltaStats
    {
        public decimal Team1WinPct { get; set; }
        public decimal Team2WinPct { get; set; }
        public double AvgDelta { get; set; }
        public double StdDevP { get; set; }
        public int SampleSize { get; set; }
    }
}
