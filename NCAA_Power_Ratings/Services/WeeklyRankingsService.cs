using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NCAA_Power_Ratings.Configuration;
using NCAA_Power_Ratings.Data;
using NCAA_Power_Ratings.Models;

namespace NCAA_Power_Ratings.Services
{
    /// <summary>
    /// Computes and persists per-week power ranking snapshots into the WeeklyRankings table.
    /// Mirrors the TeamMetricsService pipeline (SOS → PowerRating → Ranking) but scoped
    /// to games played through the specified week.
    ///
    /// Also computes per-team offensive and defensive Z-scores by decomposing each game's
    /// expected margin into expected points scored / expected points allowed, then measuring
    /// how far each team's actual scoring and defending deviated from expectation.
    ///
    /// New columns populated: AvgPointsScored, AvgPointsAllowed,
    ///                         OffensiveZScore, DefensiveZScore,
    ///                         OffensiveRank,   DefensiveRank.
    ///
    /// Call ComputeAndSaveAsync(year, week) after each week's results are finalized.
    /// </summary>
    public class WeeklyRankingsService
    {
        private readonly IDbContextFactory<NCAAContext> _contextFactory;
        private readonly MetricsConfiguration _config;

        public WeeklyRankingsService(
            IDbContextFactory<NCAAContext> contextFactory,
            IOptions<MetricsConfiguration> config)
        {
            _contextFactory = contextFactory;
            _config = config.Value;
        }

        /// <summary>
        /// Runs the full SOS → PowerRating → Ranking → Offense/Defense pipeline for all FBS teams
        /// using only games played through the specified week, then upserts the
        /// results into WeeklyRankings.
        /// </summary>
        public async Task ComputeAndSaveAsync(int year, int week, CancellationToken token = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(token);

            // ── 1. Load reference data ────────────────────────────────────────────

            var allTeams = await context.Team.ToListAsync(token);
            var fbsTeams = allTeams.Where(t => t.Division == "FBS").ToList();
            var fbsIds   = fbsTeams.Select(t => t.TeamID).ToHashSet();

            var avgScoreDeltas   = await context.AvgScoreDeltas.ToListAsync(token);
            var matchupHistories = await context.MatchupHistories.ToListAsync(token);

            // ── 2. Load only played games through this week ───────────────────────

            var games = await context.Game
                .Where(g => g.Year == year &&
                            g.Week <= week &&
                            (g.WPoints > 0 || g.LPoints > 0))
                .ToListAsync(token);

            // ── 3. Compute wins / losses / points for each team ───────────────────

            // [wins, losses, pf, pa]
            var rawStats = fbsTeams.ToDictionary(t => t.TeamID, _ => new int[4]);

            foreach (var g in games)
            {
                if (rawStats.TryGetValue(g.WinnerId, out var ws))
                {
                    ws[0]++; ws[2] += g.WPoints; ws[3] += g.LPoints;
                }
                if (rawStats.TryGetValue(g.LoserId, out var ls))
                {
                    ls[1]++; ls[2] += g.LPoints; ls[3] += g.WPoints;
                }
            }

            var winsLookup   = rawStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value[0]);
            var lossesLookup = rawStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value[1]);

            // ── 4. Build game-participant rows (winner + loser perspective) ────────

            var teamById = allTeams.ToDictionary(t => t.TeamID);

            var gameParticipants = games
                .Where(g => fbsIds.Contains(g.WinnerId) || fbsIds.Contains(g.LoserId))
                .SelectMany(g => new[]
                {
                    new GameParticipant
                    {
                        TeamId           = g.WinnerId,
                        TeamDivision     = teamById.TryGetValue(g.WinnerId, out var wt)  ? wt.Division : "FBS",
                        OpponentId       = g.LoserId,
                        OpponentDivision = teamById.TryGetValue(g.LoserId,  out var lt)  ? lt.Division : "FBS",
                        TeamPoints       = g.WPoints,
                        OpponentPoints   = g.LPoints,
                        Location         = g.Location,
                        IsHomeTeam       = g.Location == 'W'
                    },
                    new GameParticipant
                    {
                        TeamId           = g.LoserId,
                        TeamDivision     = teamById.TryGetValue(g.LoserId,  out var lt2) ? lt2.Division : "FBS",
                        OpponentId       = g.WinnerId,
                        OpponentDivision = teamById.TryGetValue(g.WinnerId, out var wt2) ? wt2.Division : "FBS",
                        TeamPoints       = g.LPoints,
                        OpponentPoints   = g.WPoints,
                        Location         = g.Location,
                        IsHomeTeam       = g.Location == 'L'
                    }
                })
                .ToList();

            // ── 5. Compute Z-scores (composite, offensive, defensive) per game ─────
            //
            // Expected margin from AvgScoreDeltas is always expressed as:
            //   higher-win-pct team scored N more points than lower-win-pct team.
            //
            // We decompose that margin into two halves to isolate each side of the ball:
            //   ExpectedTeamScore     = leagueAvgScore + (expectedDeltaFromTeamPerspective / 2)
            //   ExpectedOpponentScore = leagueAvgScore - (expectedDeltaFromTeamPerspective / 2)
            //
            // OffensiveZScore per game = (ActualTeamScore   - ExpectedTeamScore)   / StDev
            // DefensiveZScore per game = (ExpectedOppScore  - ActualOpponentScore) / StDev
            //   (positive defensive Z = held opponent below expectation = good defense)
            //
            // Both use the same rivalry-adjusted StDev as the composite Z-score so all
            // three metrics remain on the same scale.

            var homeFieldAdvantage = _config.HomeFieldAdvantage;

            // League average score — derived from games in scope so it stays consistent
            // with the data window rather than relying on a hardcoded constant.
            double leagueAvgScore = games.Count > 0
                ? (games.Average(g => (double)g.WPoints) + games.Average(g => (double)g.LPoints)) / 2.0
                : 28.0;

            var withZScores = gameParticipants.Select(gp =>
            {
                var teamWins    = winsLookup.GetValueOrDefault(gp.TeamId,     0);
                var teamLosses  = lossesLookup.GetValueOrDefault(gp.TeamId,   0);
                var oppWins     = winsLookup.GetValueOrDefault(gp.OpponentId, 0);
                var oppLosses   = lossesLookup.GetValueOrDefault(gp.OpponentId, 0);

                var teamGames = teamWins + teamLosses;
                var oppGames  = oppWins  + oppLosses;

                var teamWinPct = teamGames > 0
                    ? Math.Round((decimal)teamWins / teamGames * 20m, MidpointRounding.AwayFromZero) / 20m
                    : 0m;
                var oppWinPct = oppGames > 0
                    ? Math.Round((decimal)oppWins / oppGames * 20m, MidpointRounding.AwayFromZero) / 20m
                    : 0m;

                var maxWinPct = Math.Max(teamWinPct, oppWinPct);
                var minWinPct = Math.Min(teamWinPct, oppWinPct);

                var asd = avgScoreDeltas.FirstOrDefault(
                    a => a.Team1WinPct == maxWinPct && a.Team2WinPct == minWinPct);

                double zScore    = 0.0;
                double offZScore = 0.0;
                double defZScore = 0.0;

                if (asd != null && asd.StDevP != 0)
                {
                    // ── Expected margin from this team's perspective ───────────────
                    var expectedDelta    = (double)asd.AverageScoreDelta;
                    var expectedFromTeam = teamWinPct >= oppWinPct ? expectedDelta : -expectedDelta;

                    if (gp.IsHomeTeam)
                        expectedFromTeam += homeFieldAdvantage;
                    else if (gp.Location != 'N')
                        expectedFromTeam -= homeFieldAdvantage;

                    // ── Rivalry variance adjustment ───────────────────────────────
                    var t1 = Math.Min(gp.TeamId, gp.OpponentId);
                    var t2 = Math.Max(gp.TeamId, gp.OpponentId);
                    var rivalry = matchupHistories.FirstOrDefault(
                        m => m.Team1Id == t1 && m.Team2Id == t2);

                    var effectiveStDev = (double)asd.StDevP;
                    if (rivalry != null)
                    {
                        effectiveStDev *= rivalry.RivalryTier switch
                        {
                            "EPIC"     => 1.75,
                            "NATIONAL" => 1.50,
                            "STATE"    => 1.30,
                            "MEH"      => 1.10,
                            _          => 1.00
                        };
                    }

                    // ── Composite Z-score (unchanged from original) ───────────────
                    var delta = gp.TeamPoints - gp.OpponentPoints;
                    zScore = (delta - expectedFromTeam) / effectiveStDev;
                    if (zScore != 0)
                    {
                        var sign = Math.Sign(zScore);
                        zScore = sign * Math.Log(1 + Math.Abs(zScore));
                    }

                    // ── Decompose margin into per-side expected scores ────────────
                    // Split the expected margin symmetrically around league average.
                    // This preserves the margin relationship while giving each unit
                    // a meaningful per-game expectation to measure against.
                    var expectedTeamScore = leagueAvgScore + (expectedFromTeam / 2.0);
                    var expectedOppScore  = leagueAvgScore - (expectedFromTeam / 2.0);

                    // ── Offensive Z-score ─────────────────────────────────────────
                    // Positive = scored more than expected for this matchup context.
                    var rawOffZ = (gp.TeamPoints - expectedTeamScore) / effectiveStDev;
                    offZScore = rawOffZ != 0
                        ? Math.Sign(rawOffZ) * Math.Log(1 + Math.Abs(rawOffZ))
                        : 0.0;

                    // ── Defensive Z-score ─────────────────────────────────────────
                    // Positive = held opponent below what was expected (good defense).
                    var rawDefZ = (expectedOppScore - gp.OpponentPoints) / effectiveStDev;
                    defZScore = rawDefZ != 0
                        ? Math.Sign(rawDefZ) * Math.Log(1 + Math.Abs(rawDefZ))
                        : 0.0;
                }

                var divWeight = gp.OpponentDivision == "FCS" ? 0.25 : 1.0;

                return new
                {
                    gp.TeamId,
                    gp.TeamDivision,
                    gp.OpponentId,
                    gp.OpponentDivision,
                    ZScore    = zScore,
                    OffZScore = offZScore,
                    DefZScore = defZScore,
                    DivWeight = divWeight,
                    PerfWeight = zScore switch
                    {
                        >= 1.0 => 1.25,
                        > -1.0 => 1.00,
                        > -2.0 => 0.75,
                        _      => 0.50
                    }
                };
            }).ToList();

            // ── 6. Compute BaseSOS ────────────────────────────────────────────────

            var baseSOS = withZScores
                .GroupBy(x => x.TeamId)
                .Select(g => new
                {
                    TeamId  = g.Key,
                    BaseSOS = Math.Round(
                        g.Sum(x => x.PerfWeight * x.DivWeight) /
                        g.Sum(x => x.DivWeight),
                        3)
                })
                .ToDictionary(x => x.TeamId, x => x.BaseSOS);

            // ── 7. Compute SubSOS (opponents' BaseSOS) ────────────────────────────

            var subSOS = withZScores
                .GroupBy(x => x.TeamId)
                .Select(g => new
                {
                    TeamId = g.Key,
                    SubSOS = g.Sum(x => x.DivWeight) > 0
                        ? Math.Round(
                            g.Sum(x => baseSOS.GetValueOrDefault(x.OpponentId, 0.0) * x.PerfWeight) /
                            g.Sum(x => x.PerfWeight),
                            3)
                        : 0.0
                })
                .ToDictionary(x => x.TeamId, x => x.SubSOS);

            // ── 8. CombinedSOS = 40% Base + 60% Sub ──────────────────────────────

            var combinedSOS = fbsTeams.ToDictionary(
                t => t.TeamID,
                t =>
                {
                    var b = baseSOS.GetValueOrDefault(t.TeamID, 0.0);
                    var s = subSOS.GetValueOrDefault(t.TeamID, b);
                    return Math.Round((2 * b + 3 * s) / 5.0, 4);
                });

            // ── 9. PowerRating = avg Z-score (weighted) × CombinedSOS ────────────

            var powerRatings = withZScores
                .GroupBy(x => x.TeamId)
                .Select(g => new
                {
                    TeamId      = g.Key,
                    PowerRating = g.Sum(x => x.DivWeight) > 0
                        ? Math.Round(
                            g.Sum(x => x.ZScore * x.DivWeight) /
                            g.Sum(x => x.DivWeight) *
                            combinedSOS.GetValueOrDefault(g.Key, 1.0),
                            4)
                        : 0.0
                })
                .ToDictionary(x => x.TeamId, x => x.PowerRating);

            // ── 10. Ranking = WinPct × CombinedSOS × (1 + PowerRating) ───────────

            var rankings = fbsTeams
                .Select(t =>
                {
                    var wins   = winsLookup.GetValueOrDefault(t.TeamID, 0);
                    var losses = lossesLookup.GetValueOrDefault(t.TeamID, 0);
                    var total  = wins + losses;

                    if (total == 0)
                        return new { t.TeamID, Ranking = (decimal?)null };

                    var winPct = (decimal)wins / total;
                    var sos    = (decimal)combinedSOS.GetValueOrDefault(t.TeamID, 0.0);
                    var pr     = (decimal)powerRatings.GetValueOrDefault(t.TeamID, 0.0);

                    return new
                    {
                        t.TeamID,
                        Ranking = (decimal?)Math.Round(winPct * sos * (1 + pr), 4)
                    };
                })
                .ToDictionary(x => x.TeamID, x => x.Ranking);

            // ── 11. Assign OverallRank and TierRank ───────────────────────────────

            var getTier = (Team t) => GetConferenceTier(t.Conference, t.TeamName);

            var ranked = fbsTeams
                .Where(t => rankings[t.TeamID].HasValue)
                .OrderByDescending(t => rankings[t.TeamID])
                .Select((t, i) => new { Team = t, OverallRank = i + 1, Tier = getTier(t) })
                .ToList();

            var tierRanks = new Dictionary<int, int>();
            foreach (var tierGroup in ranked.GroupBy(x => x.Tier))
            {
                int idx = 1;
                foreach (var x in tierGroup.OrderByDescending(x => rankings[x.Team.TeamID]))
                    tierRanks[x.Team.TeamID] = idx++;
            }

            // ── 12. Compute per-team offensive / defensive Z-scores ───────────────
            //
            // Division-weighted averages keep FCS cupcake games at 25% weight,
            // consistent with the composite Z-score treatment.

            var offensiveZScores = withZScores
                .GroupBy(x => x.TeamId)
                .Select(g => new
                {
                    TeamId          = g.Key,
                    OffensiveZScore = g.Sum(x => x.DivWeight) > 0
                        ? Math.Round(
                            g.Sum(x => x.OffZScore * x.DivWeight) /
                            g.Sum(x => x.DivWeight),
                            4)
                        : 0.0
                })
                .ToDictionary(x => x.TeamId, x => x.OffensiveZScore);

            var defensiveZScores = withZScores
                .GroupBy(x => x.TeamId)
                .Select(g => new
                {
                    TeamId          = g.Key,
                    DefensiveZScore = g.Sum(x => x.DivWeight) > 0
                        ? Math.Round(
                            g.Sum(x => x.DefZScore * x.DivWeight) /
                            g.Sum(x => x.DivWeight),
                            4)
                        : 0.0
                })
                .ToDictionary(x => x.TeamId, x => x.DefensiveZScore);

            // ── 13. Rank offense and defense among FBS teams with games played ─────
            //
            // Teams with no games played receive rank 0.
            // Higher Z-score = better unit = lower ordinal rank number (1 = best).

            var fbsWithGames = fbsTeams
                .Where(t => (rawStats[t.TeamID][0] + rawStats[t.TeamID][1]) > 0)
                .ToList();

            var offensiveRanks = fbsWithGames
                .OrderByDescending(t => offensiveZScores.GetValueOrDefault(t.TeamID, 0.0))
                .Select((t, i) => new { t.TeamID, Rank = i + 1 })
                .ToDictionary(x => x.TeamID, x => x.Rank);

            var defensiveRanks = fbsWithGames
                .OrderByDescending(t => defensiveZScores.GetValueOrDefault(t.TeamID, 0.0))
                .Select((t, i) => new { t.TeamID, Rank = i + 1 })
                .ToDictionary(x => x.TeamID, x => x.Rank);

            // ── 14. Upsert into WeeklyRankings ────────────────────────────────────

            var existing = await context.WeeklyRankings
                .Where(wr => wr.Year == year && wr.Week == week)
                .ToDictionaryAsync(wr => wr.TeamID, token);

            foreach (var t in fbsTeams)
            {
                var s           = rawStats[t.TeamID];
                var rank        = ranked.FirstOrDefault(r => r.Team.TeamID == t.TeamID);
                var gamesPlayed = s[0] + s[1];

                // Per-game averages — null when no games played to avoid divide-by-zero
                decimal avgPtsScored  = gamesPlayed > 0 ? Math.Round((decimal)s[2] / gamesPlayed, 2) : 0;
                decimal avgPtsAllowed = gamesPlayed > 0 ? Math.Round((decimal)s[3] / gamesPlayed, 2) : 0;

                decimal offZ = gamesPlayed > 0
                    ? (decimal)Math.Round(offensiveZScores.GetValueOrDefault(t.TeamID, 0.0), 4)
                    : 0;
                decimal defZ = gamesPlayed > 0
                    ? (decimal)Math.Round(defensiveZScores.GetValueOrDefault(t.TeamID, 0.0), 4)
                    : 0;

                int offRank = offensiveRanks.GetValueOrDefault(t.TeamID, 0);
                int defRank = defensiveRanks.GetValueOrDefault(t.TeamID, 0);

                if (existing.TryGetValue(t.TeamID, out var row))
                {
                    row.Wins             = (byte)s[0];
                    row.Losses           = (byte)s[1];
                    row.PointsFor        = s[2];
                    row.PointsAgainst    = s[3];
                    row.BaseSOS          = (decimal?)baseSOS.GetValueOrDefault(t.TeamID);
                    row.SubSOS           = (decimal?)subSOS.GetValueOrDefault(t.TeamID);
                    row.CombinedSOS      = (decimal?)combinedSOS.GetValueOrDefault(t.TeamID);
                    row.PowerRating      = (decimal?)powerRatings.GetValueOrDefault(t.TeamID);
                    row.Ranking          = rankings[t.TeamID];
                    row.OverallRank      = rank?.OverallRank ?? 0;
                    row.TierRank         = tierRanks.GetValueOrDefault(t.TeamID, 0);
                    row.AvgPointsScored  = avgPtsScored;
                    row.AvgPointsAllowed = avgPtsAllowed;
                    row.OffensiveZScore  = offZ;
                    row.DefensiveZScore  = defZ;
                    row.OffensiveRank    = offRank;
                    row.DefensiveRank    = defRank;
                }
                else
                {
                    context.WeeklyRankings.Add(new WeeklyRanking
                    {
                        TeamID           = t.TeamID,
                        Year             = (short)year,
                        Week             = (byte)week,
                        Wins             = (byte)s[0],
                        Losses           = (byte)s[1],
                        PointsFor        = s[2],
                        PointsAgainst    = s[3],
                        BaseSOS          = (decimal?)baseSOS.GetValueOrDefault(t.TeamID),
                        SubSOS           = (decimal?)subSOS.GetValueOrDefault(t.TeamID),
                        CombinedSOS      = (decimal?)combinedSOS.GetValueOrDefault(t.TeamID),
                        PowerRating      = (decimal?)powerRatings.GetValueOrDefault(t.TeamID),
                        Ranking          = rankings[t.TeamID],
                        OverallRank      = rank?.OverallRank ?? 0,
                        TierRank         = tierRanks.GetValueOrDefault(t.TeamID, 0),
                        AvgPointsScored  = avgPtsScored,
                        AvgPointsAllowed = avgPtsAllowed,
                        OffensiveZScore  = offZ,
                        DefensiveZScore  = defZ,
                        OffensiveRank    = offRank,
                        DefensiveRank    = defRank
                    });
                }
            }

            await context.SaveChangesAsync(token);
        }

        /// <summary>
        /// Backfills all weeks for a given year in order.
        /// Useful for seeding historical data.
        /// </summary>
        public async Task BackfillYearAsync(int year, CancellationToken token = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(token);

            var playedWeeks = await context.Game
                .Where(g => g.Year == year && (g.WPoints > 0 || g.LPoints > 0))
                .Select(g => g.Week)
                .Distinct()
                .OrderBy(w => w)
                .ToListAsync(token);

            foreach (var week in playedWeeks)
            {
                await ComputeAndSaveAsync(year, week, token);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string GetConferenceTier(string? conference, string? teamName = null)
        {
            if (!string.IsNullOrEmpty(teamName))
            {
                if (teamName.Equals("Notre Dame",   StringComparison.OrdinalIgnoreCase)) return "P4";
                if (teamName.Equals("Connecticut",  StringComparison.OrdinalIgnoreCase)) return "G5";
            }

            if (string.IsNullOrEmpty(conference)) return "Other";

            var power4 = new[]
            {
                "SEC", "Southeastern Conference",
                "Big Ten", "Big Ten Conference",
                "Big 12", "Big 12 Conference",
                "ACC", "Atlantic Coast Conference"
            };
            if (power4.Any(p => conference.Contains(p, StringComparison.OrdinalIgnoreCase))) return "P4";

            var group5 = new[]
            {
                "American Athletic", "American Athletic Conference", "AAC",
                "Mountain West", "Mountain West Conference",
                "Sun Belt", "Sun Belt Conference",
                "Mid-American", "Mid-American Conference", "MAC",
                "Conference USA", "C-USA",
                "Pac-12", "Pac-12 Conference"
            };
            if (group5.Any(g => conference.Contains(g, StringComparison.OrdinalIgnoreCase))) return "G5";

            if (conference.Contains("Independent", StringComparison.OrdinalIgnoreCase)) return "Independent";

            return "Other";
        }

        // ── Private helper type ───────────────────────────────────────────────────

        private class GameParticipant
        {
            public int    TeamId           { get; set; }
            public string TeamDivision     { get; set; } = "";
            public int    OpponentId       { get; set; }
            public string OpponentDivision { get; set; } = "";
            public int    TeamPoints       { get; set; }
            public int    OpponentPoints   { get; set; }
            public char   Location         { get; set; }
            public bool   IsHomeTeam       { get; set; }
        }
    }
}
