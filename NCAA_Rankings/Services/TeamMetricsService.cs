using Microsoft.EntityFrameworkCore;
using NCAA_Rankings.Data;
using NCAA_Rankings.Interfaces;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NCAA_Rankings.Services
{
    /// <summary>
    /// Service for calculating team performance metrics and trends.
    /// </summary>
    public class TeamMetricsService
    {
        private readonly IDbContextFactory<NCAAContext> _contextFactory;

        public TeamMetricsService(IDbContextFactory<NCAAContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// Calculates team performance trends based on the last 10 years of data.
        /// Returns normalized win percentages (oldest to newest) plus weighted average and projected wins.
        /// Normalizes for 12-game seasons: <=12 games uses actual percentage, >12 games gets 0.25 bump per extra win.
        /// Most recent year receives the highest weight in the weighted average calculation.
        /// Projected wins = weighted average × 12, rounded to nearest integer using 0.75 as boundary.
        /// </summary>
        public async Task<string> CalculateTrend(CancellationToken token = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(token);

            try
            {
                var standardSeasonGames = 12; //default to current standard; actual season length can vary by year, but we normalize to 12 for projection
                const double extraWinBump = 0.25;

                var currentYear = DateTime.Now.Year;
                var startYear = currentYear - 10;

                // Get team records from last 10 years
                var teamRecords = await context.TeamRecords
                    .Where(tr => tr.Year >= startYear)
                    .Include(tr => tr.Team)
                    .ToListAsync(token);

                // Group by team and calculate normalized win percentages with weighted average
                var results = teamRecords
                    .GroupBy(tr => tr.TeamID)
                    .Select(g =>
                    {
                        // Order by year ascending (oldest to newest), take last 10
                        var records = g.OrderBy(r => r.Year).TakeLast(10).ToList();

                        // Calculate normalized win percentage for each year (oldest to newest)
                        var normalizedPercentages = records.Select(r =>
                        {
                            standardSeasonGames = r.RegularSeasonGames; // Get standard season games for the year

                            int totalGames = r.Wins + r.Losses;
                            if (totalGames <= 0)
                                return 0.0;

                            double normalized;
                            if (totalGames <= standardSeasonGames)
                            {
                                // Use actual win percentage
                                normalized = (double)r.Wins / totalGames;
                            }
                            else
                            {
                                // 12-game denominator with 0.25 bump for each extra win
                                int baseWins = Math.Min(r.Wins, standardSeasonGames);
                                int extraWins = Math.Max(0, r.Wins - standardSeasonGames);
                                normalized = (baseWins + (extraWins * extraWinBump)) / standardSeasonGames;
                            }

                            return Math.Round(normalized, 4);
                        }).ToList();

                        // Calculate weighted average (most recent year = highest weight)
                        double weightedAverage = 0.0;
                        if (normalizedPercentages.Count > 0)
                        {
                            int n = normalizedPercentages.Count;
                            long weightSum = (long)n * (n + 1) / 2;

                            // Reverse iterate to give highest weight to most recent (last in array)
                            weightedAverage = normalizedPercentages
                                .Select((pct, index) => pct * (index + 1)) // index 0 = oldest = weight 1, last = weight n
                                .Sum() / weightSum;
                        }

                        // Calculate projected wins (weighted average × 12)
                        double projectedWinsDecimal = weightedAverage * standardSeasonGames;

                        // Round using 0.75 as boundary: if fractional part >= 0.75, round up; otherwise round down
                        int projectedWins = projectedWinsDecimal - Math.Floor(projectedWinsDecimal) >= 0.75
                            ? (int)Math.Ceiling(projectedWinsDecimal)
                            : (int)Math.Floor(projectedWinsDecimal);

                        return new
                        {
                            TeamID = g.Key,
                            TeamName = g.First().Team?.TeamName ?? "Unknown",
                            Years = records.Select(r => r.Year).ToList(), // Oldest to newest
                            NormalizedWinPercentages = normalizedPercentages, // Oldest to newest
                            WeightedAverage = Math.Round(weightedAverage, 4),
                            ProjectedWins = projectedWins,
                            RecordCount = records.Count
                        };
                    })
                    .OrderByDescending(r => r.ProjectedWins).OrderByDescending(r => r.WeightedAverage)
                    .ToList();

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                return JsonSerializer.Serialize(results, options);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating team trends: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Calculates and sets Strength of Schedule (SOS) values for all teams in the specified year.
        /// Computes BaseSOS (first-order), SubSOS (second-order), and CombinedSOS (weighted blend).
        /// </summary>
        /// <param name="year">The year to calculate SOS for. Defaults to current year.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task SetSOS(int? year = null, CancellationToken token = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(token);

            try
            {
                var targetYear = year ?? DateTime.Now.Year;

                // Step 1: GameParticipants - Union of games from winner and loser perspectives
                var gamesFromWinner = context.Games
                    .Where(g => g.Year == targetYear)
                    .Select(g => new
                    {
                        g.Year,
                        TeamId = g.WinnerId,
                        TeamName = g.WinnerName,
                        OpponentId = g.LoserId,
                        OpponentName = g.LoserName,
                        TeamPoints = g.WPoints,
                        OpponentPoints = g.LPoints
                    });

                var gamesFromLoser = context.Games
                    .Where(g => g.Year == targetYear)
                    .Select(g => new
                    {
                        g.Year,
                        TeamId = g.LoserId,
                        TeamName = g.LoserName,
                        OpponentId = g.WinnerId,
                        OpponentName = g.WinnerName,
                        TeamPoints = g.LPoints,
                        OpponentPoints = g.WPoints
                    });

                var gameParticipants = await gamesFromWinner
                    .Union(gamesFromLoser)
                    .ToListAsync(token);

                // Step 2: Join with TeamRecords to get wins
                var withRecords = gameParticipants
                    .Join(
                        context.TeamRecords.Where(tr => tr.Year == targetYear),
                        gp => gp.TeamId,
                        tr => tr.TeamID,
                        (gp, tr) => new { gp, TeamWins = tr.Wins }
                    )
                    .Join(
                        context.TeamRecords.Where(tr => tr.Year == targetYear),
                        x => x.gp.OpponentId,
                        tr => tr.TeamID,
                        (x, tr) => new
                        {
                            x.gp.Year,
                            x.gp.TeamId,
                            x.gp.TeamName,
                            x.TeamWins,
                            x.gp.OpponentId,
                            x.gp.OpponentName,
                            x.gp.TeamPoints,
                            x.gp.OpponentPoints,
                            OpponentWins = tr.Wins
                        }
                    )
                    .ToList();

                // Step 3: Join with AvgScoreDeltas and calculate Z-values
                var avgScoreDeltas = await context.AvgScoreDeltas.ToListAsync(token);

                var withDeltas = withRecords
                    .Select(r =>
                    {
                        var delta = r.TeamPoints - r.OpponentPoints;
                        var maxWins = Math.Max(r.TeamWins, r.OpponentWins);
                        var minWins = Math.Min(r.TeamWins, r.OpponentWins);

                        var asd = avgScoreDeltas.FirstOrDefault(a =>
                            a.Team1Wins == maxWins && a.Team2Wins == minWins);

                        double zValue = 0.0;
                        if (asd != null && asd.StDevP != 0)
                        {
                            zValue = ((double)delta - asd.AverageScoreDelta) / (double)asd.StDevP;
                        }

                        return new
                        {
                            r.Year,
                            r.TeamId,
                            r.TeamName,
                            r.TeamWins,
                            r.OpponentId,
                            r.OpponentName,
                            r.TeamPoints,
                            r.OpponentPoints,
                            Delta = delta,
                            ZValue = zValue
                        };
                    })
                    .ToList();

                // Step 4: Assign weights based on Z-values
                var withWeights = withDeltas
                    .Select(d => new
                    {
                        d.Year,
                        d.TeamId,
                        d.TeamName,
                        d.OpponentId,
                        d.OpponentName,
                        Weight = d.ZValue switch
                        {
                            >= 1.0 => 1.25,
                            > -1.0 => 1.00,
                            > -2.0 => 0.75,
                            _ => 0.50
                        }
                    })
                    .ToList();

                // Step 5: Calculate BaseSOS per team
                var baseSOS = withWeights
                    .GroupBy(w => new { w.Year, w.TeamId })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.TeamId,
                        BaseSOS = Math.Round(g.Sum(x => x.Weight) / g.Count(), 3),
                        GamesPlayed = g.Count()
                    })
                    .ToList();

                // Step 6: Calculate OpponentSOS (join weights with opponents' BaseSOS)
                var opponentSOS = withWeights
                    .Join(
                        baseSOS,
                        w => new { w.Year, TeamId = w.OpponentId },
                        b => new { Year = b.Year, b.TeamId },
                        (w, b) => new
                        {
                            w.Year,
                            w.TeamId,
                            OppBaseSOS = b.BaseSOS,
                            w.Weight
                        }
                    )
                    .ToList();

                // Step 7: Calculate SecondOrderSOS (SubSOS)
                var secondOrderSOS = opponentSOS
                    .GroupBy(o => new { o.Year, o.TeamId })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.TeamId,
                        SubSOS = Math.Round(
                            g.Sum(x => x.OppBaseSOS * x.Weight) / g.Sum(x => x.Weight),
                            3
                        )
                    })
                    .ToList();

                // Step 8: Combine BaseSOS and SubSOS
                var combined = baseSOS
                    .GroupJoin(
                        secondOrderSOS,
                        b => new { b.Year, b.TeamId },
                        s => new { s.Year, s.TeamId },
                        (b, s) => new
                        {
                            b.Year,
                            b.TeamId,
                            b.BaseSOS,
                            SubSOS = s.FirstOrDefault()?.SubSOS ?? b.BaseSOS,
                        }
                    )
                    .Select(c => new
                    {
                        c.Year,
                        c.TeamId,
                        c.BaseSOS,
                        c.SubSOS,
                        CombinedSOS = Math.Round((2 * c.BaseSOS + c.SubSOS) / 3, 4)
                    })
                    .ToList();

                // Step 9: Update TeamRecords
                var teamRecordsToUpdate = await context.TeamRecords
                    .Where(tr => tr.Year == targetYear)
                    .ToListAsync(token);

                foreach (var record in teamRecordsToUpdate)
                {
                    var sosData = combined.FirstOrDefault(c =>
                        c.TeamId == record.TeamID && c.Year == record.Year);

                    if (sosData != null)
                    {
                        record.BaseSOS = (decimal)sosData.BaseSOS;
                        record.SubSOS = (decimal)sosData.SubSOS;
                        record.CombinedSOS = (decimal)sosData.CombinedSOS;
                    }
                }

                await context.SaveChangesAsync(token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating SOS: {ex.Message}");
                throw;
            }
        }
    }
}