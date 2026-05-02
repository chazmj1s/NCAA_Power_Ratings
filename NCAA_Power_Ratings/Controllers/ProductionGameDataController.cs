using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NCAA_Power_Ratings.Data;
using NCAA_Power_Ratings.Services;

namespace NCAA_Power_Ratings.Controllers
{
    /// <summary>
    /// Production API for game predictions and team data queries.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ProductionGameDataController(
        GamePredictionService gamePredictionService,
        IDbContextFactory<NCAAContext> contextFactory,
        ILogger<ProductionGameDataController> logger) : ControllerBase
    {
        /// <summary>
        /// Predicts the score for a single matchup between two teams.
        /// Location: 'H' = team is home, 'A' = team is away, 'N' = neutral site
        /// 
        /// Example: GET /api/productiongamedata/predictMatchup?year=2025&teamName=Ohio State&opponentName=Michigan&location=H&week=12
        /// </summary>
        [HttpGet("predictMatchup")]
        public async Task<IActionResult> PredictMatchup(
            [FromQuery] int? year,
            [FromQuery] string teamName,
            [FromQuery] string opponentName,
            [FromQuery] char location = 'N',
            [FromQuery] int week = 0)
        {
            try
            {
                if (string.IsNullOrEmpty(teamName) || string.IsNullOrEmpty(opponentName))
                {
                    return BadRequest("Both teamName and opponentName are required");
                }

                var targetYear = year ?? DateTime.Now.Year;

                var prediction = await gamePredictionService.PredictMatchup(
                    targetYear, teamName, opponentName, location, week);

                return Ok(new
                {
                    matchup = $"{prediction.TeamName} {prediction.LocationDisplay} {prediction.OpponentName}",
                    prediction = $"{prediction.TeamName} {prediction.PredictedTeamScore:F1}, {prediction.OpponentName} {prediction.PredictedOpponentScore:F1}",
                    expectedMargin = prediction.ExpectedMargin,
                    marginOfError = prediction.MarginOfError,
                    confidence = prediction.Confidence,
                    teamRecord = $"{prediction.TeamWins}-?",
                    opponentRecord = $"{prediction.OpponentWins}-?",
                    teamPowerRating = prediction.TeamPowerRating,
                    opponentPowerRating = prediction.OpponentPowerRating,
                    rivalryNote = prediction.RivalryNote,
                    summary = prediction.PredictionSummary
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error predicting matchup");
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        /// Predicts scores for multiple matchups provided in the request body.
        /// POST body example:
        /// {
        ///   "year": 2025,
        ///   "matchups": [
        ///     { "teamName": "Ohio State", "opponentName": "Michigan", "location": "H", "week": 12 },
        ///     { "teamName": "Alabama", "opponentName": "Auburn", "location": "N", "week": 13 }
        ///   ]
        /// }
        /// </summary>
        [HttpPost("predictMatchups")]
        public async Task<IActionResult> PredictMatchups([FromBody] MatchupBatchRequest request)
        {
            try
            {
                var predictions = await gamePredictionService.PredictMatchups(
                    request.Year, request.Matchups);

                return Ok(new
                {
                    message = $"Predicted {predictions.Count} matchups for {request.Year}",
                    predictions = predictions.Select(p => new
                    {
                        matchup = $"{p.TeamName} {p.LocationDisplay} {p.OpponentName}",
                        prediction = $"{p.TeamName} {p.PredictedTeamScore:F1}, {p.OpponentName} {p.PredictedOpponentScore:F1}",
                        expectedMargin = p.ExpectedMargin,
                        marginOfError = p.MarginOfError,
                        confidence = p.Confidence,
                        rivalryNote = p.RivalryNote,
                        summary = p.PredictionSummary
                    })
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error predicting matchups");
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        /// Diagnostic endpoint to check database data availability
        /// GET /api/productiongamedata/diagnostic
        /// </summary>
        [HttpGet("diagnostic")]
        public async Task<IActionResult> GetDiagnostic()
        {
            try
            {
                await using var context = await contextFactory.CreateDbContextAsync();

                var totalTeams = await context.Teams.CountAsync();
                var totalGames = await context.Games.CountAsync();
                var totalRecords = await context.TeamRecords.CountAsync();
                var recordsWithPowerRating = await context.TeamRecords.CountAsync(tr => tr.PowerRating.HasValue);

                var years = await context.TeamRecords
                    .Where(tr => tr.PowerRating.HasValue)
                    .Select(tr => tr.Year)
                    .Distinct()
                    .OrderBy(y => y)
                    .ToListAsync();

                var yearStats = new List<object>();
                foreach (var year in years)
                {
                    var count = await context.TeamRecords
                        .CountAsync(tr => tr.Year == year && tr.PowerRating.HasValue);
                    yearStats.Add(new { year, teamsWithRankings = count });
                }

                return Ok(new
                {
                    database = "Connected",
                    totalTeams,
                    totalGames,
                    totalRecords,
                    recordsWithPowerRating,
                    yearsWithData = years,
                    yearStats
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting diagnostic info");
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        /// Query team records with filters for wins, losses, year range, and PowerRating range.
        /// Example: GET /api/productiongamedata/queryTeamRecords?wins=13&losses=3
        /// Example: GET /api/productiongamedata/queryTeamRecords?minPowerRating=-0.02&maxPowerRating=0.01
        /// Example: GET /api/productiongamedata/queryTeamRecords?startYear=2020&endYear=2024&minWins=10
        /// </summary>
        [HttpGet("queryTeamRecords")]
        public async Task<IActionResult> QueryTeamRecords(
            [FromQuery] int? wins,
            [FromQuery] int? losses,
            [FromQuery] int? minWins,
            [FromQuery] int? maxWins,
            [FromQuery] int? startYear,
            [FromQuery] int? endYear,
            [FromQuery] decimal? minPowerRating,
            [FromQuery] decimal? maxPowerRating,
            [FromQuery] int limit = 50)
        {
            try
            {
                await using var context = await contextFactory.CreateDbContextAsync();

                var query = context.TeamRecords
                    .Include(tr => tr.Team)
                    .Where(tr => tr.PowerRating != null);

                // Apply filters
                if (wins.HasValue)
                    query = query.Where(tr => tr.Wins == wins.Value);

                if (losses.HasValue)
                    query = query.Where(tr => tr.Losses == losses.Value);

                if (minWins.HasValue)
                    query = query.Where(tr => tr.Wins >= minWins.Value);

                if (maxWins.HasValue)
                    query = query.Where(tr => tr.Wins <= maxWins.Value);

                if (startYear.HasValue)
                    query = query.Where(tr => tr.Year >= startYear.Value);

                if (endYear.HasValue)
                    query = query.Where(tr => tr.Year <= endYear.Value);

                if (minPowerRating.HasValue)
                    query = query.Where(tr => tr.PowerRating >= minPowerRating.Value);

                if (maxPowerRating.HasValue)
                    query = query.Where(tr => tr.PowerRating <= maxPowerRating.Value);

                var results = await query
                    .OrderByDescending(tr => tr.Year)
                    .ThenByDescending(tr => tr.PowerRating)
                    .Take(limit)
                    .Select(tr => new
                    {
                        tr.Year,
                        TeamName = tr.Team!.TeamName,
                        Record = $"{tr.Wins}-{tr.Losses}",
                        tr.Wins,
                        tr.Losses,
                        tr.PointsFor,
                        tr.PointsAgainst,
                        PointDifferential = tr.PointsFor - tr.PointsAgainst,
                        tr.BaseSOS,
                        tr.SubSOS,
                        tr.CombinedSOS,
                        tr.PowerRating
                    })
                    .ToListAsync();

                return Ok(new
                {
                    count = results.Count,
                    filters = new
                    {
                        wins,
                        losses,
                        minWins,
                        maxWins,
                        startYear,
                        endYear,
                        minPowerRating,
                        maxPowerRating,
                        limit
                    },
                    results
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error querying team records");
                return StatusCode(500, "An error occurred while querying team records.");
            }
        }

        /// <summary>
        /// Query matchup histories and detected rivalries.
        /// Omit all parameters or pass tier=ALL to get all matchups.
        /// Example: GET /api/productiongamedata/rivalries?tier=EPIC&minGames=50
        /// Example: GET /api/productiongamedata/rivalries (returns all)
        /// </summary>
        [HttpGet("rivalries")]
        public async Task<IActionResult> GetRivalries(
            [FromQuery] string? tier,
            [FromQuery] int? minGames,
            [FromQuery] double? minVarianceRatio)
        {
            try
            {
                await using var context = await contextFactory.CreateDbContextAsync();

                var query = context.MatchupHistories.AsQueryable();

                // Filter by tier if specified (and not "ALL")
                if (!string.IsNullOrEmpty(tier) && !tier.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(m => m.RivalryTier == tier);
                }

                // Filter by minimum games
                if (minGames.HasValue)
                {
                    query = query.Where(m => m.GamesPlayed >= minGames.Value);
                }

                var matchups = await query
                    .OrderByDescending(m => m.GamesPlayed)
                    .ToListAsync();

                logger.LogInformation("Found {Count} matchups matching filters", matchups.Count);

                // Get team names lookup in batch
                var teamIds = matchups.SelectMany(m => new[] { m.Team1Id, m.Team2Id }).Distinct().ToList();
                var teamNames = await context.Teams
                    .Where(t => teamIds.Contains(t.TeamID))
                    .ToDictionaryAsync(t => t.TeamID, t => t.TeamName);

                // Calculate average StDev once
                var avgScoreDeltas = await context.AvgScoreDeltas.ToListAsync();
                var avgStDev = avgScoreDeltas.Any() ? avgScoreDeltas.Average(a => (double)a.StDevP) : 15.0;

                // Build results
                var results = new List<object>();

                foreach (var matchup in matchups)
                {
                    var team1 = teamNames.GetValueOrDefault(matchup.Team1Id, "Unknown");
                    var team2 = teamNames.GetValueOrDefault(matchup.Team2Id, "Unknown");

                    // Calculate variance ratio
                    var varianceRatio = (double)matchup.StDevMargin / avgStDev;

                    // Apply minimum variance ratio filter if specified
                    if (minVarianceRatio.HasValue && varianceRatio < minVarianceRatio.Value)
                    {
                        continue;
                    }

                    results.Add(new
                    {
                        team1,
                        team2,
                        rivalryName = matchup.RivalryName ?? "N/A",
                        tier = matchup.RivalryTier ?? "N/A",
                        gamesPlayed = matchup.GamesPlayed,
                        avgMargin = Math.Round((double)matchup.AvgMargin, 1),
                        stDevMargin = Math.Round((double)matchup.StDevMargin, 1),
                        upsetRate = Math.Round((double)matchup.UpsetRate, 3),
                        varianceRatio = Math.Round(varianceRatio, 2),
                        seriesAge = matchup.LastPlayed - matchup.FirstPlayed,
                        firstPlayed = matchup.FirstPlayed,
                        lastPlayed = matchup.LastPlayed
                    });
                }

                return Ok(new
                {
                    totalMatchups = results.Count,
                    totalInDatabase = matchups.Count,
                    filters = new
                    {
                        tier = tier ?? "ALL",
                        minGames = minGames ?? 0,
                        minVarianceRatio = minVarianceRatio ?? 0.0
                    },
                    rivalries = results
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error querying rivalries");
                return StatusCode(500, "An error occurred while querying rivalries.");
            }
        }

        /// <summary>
        /// Get power rankings for a specific year.
        /// Example: GET /api/productiongamedata/powerrankings?year=2025
        /// </summary>
        [HttpGet("powerrankings")]
        public async Task<IActionResult> GetPowerRankings([FromQuery] int? year)
        {
            try
            {
                var targetYear = year ?? DateTime.Now.Year;

                await using var context = await contextFactory.CreateDbContextAsync();

                // Load data first, then order in memory (SQLite doesn't support ordering by decimal)
                var teamRecords = await context.TeamRecords
                    .Include(tr => tr.Team)
                    .Where(tr => tr.Year == targetYear && tr.Ranking.HasValue)
                    .ToListAsync();

                // Add conference tier to each team
                var teamsWithTiers = teamRecords
                    .Select(tr => new
                    {
                        TeamRecord = tr,
                        Tier = GetConferenceTier(tr.Team?.Conference, tr.Team?.TeamName)
                    })
                    .OrderByDescending(t => t.TeamRecord.Ranking)
                    .ToList();

                // Calculate overall rank (all teams)
                var withOverallRank = teamsWithTiers
                    .Select((t, index) => new
                    {
                        t.TeamRecord,
                        t.Tier,
                        OverallRank = index + 1
                    })
                    .ToList();

                // Group by tier and calculate tier-specific ranks
                var tierGroups = withOverallRank.GroupBy(t => t.Tier).ToList();

                // Dictionary to hold tier ranks
                var tierRankLookup = new Dictionary<int, int>(); // TeamID -> TierRank

                foreach (var tierGroup in tierGroups)
                {
                    var tieredTeams = tierGroup
                        .OrderByDescending(t => t.TeamRecord.Ranking)
                        .Select((t, index) => new { t.TeamRecord.TeamID, TierRank = index + 1 })
                        .ToList();

                    foreach (var team in tieredTeams)
                    {
                        tierRankLookup[team.TeamID] = team.TierRank;
                    }
                }

                // Build final rankings list
                var rankings = withOverallRank
                    .OrderByDescending(t => t.TeamRecord.Ranking)
                    .Select(t => new
                    {
                        TeamID = t.TeamRecord.TeamID,
                        TeamName = t.TeamRecord.Team!.TeamName,
                        Conference = t.TeamRecord.Team.Conference,
                        ConferenceAbbr = t.TeamRecord.Team.ConferenceAbbr,
                        Division = t.TeamRecord.Team.Division,
                        Tier = t.Tier,
                        OverallRank = t.OverallRank,                        // Rank among ALL teams
                        TierRank = tierRankLookup[t.TeamRecord.TeamID],    // Rank within tier (P4, G5, etc.)
                        Ranking = t.TeamRecord.Ranking,
                        Year = t.TeamRecord.Year,
                        Wins = t.TeamRecord.Wins,
                        Losses = t.TeamRecord.Losses,
                        BaseSOS = t.TeamRecord.BaseSOS,
                        CombinedSOS = t.TeamRecord.CombinedSOS
                    })
                    .ToList();

                logger.LogInformation("Found {Count} teams with power ratings for year {Year}", rankings.Count, targetYear);

                return Ok(rankings);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving power rankings");
                return StatusCode(500, "An error occurred while retrieving power rankings.");
            }
        }

        /// <summary>
        /// Get the full schedule for a season with actual and projected scores/O-U.
        /// Example: GET /api/productiongamedata/schedule?year=2025
        /// </summary>
        [HttpGet("schedule")]
        public async Task<IActionResult> GetSchedule([FromQuery] int? year)
        {
            try
            {
                var targetYear = year ?? DateTime.Now.Year;

                await using var context = await contextFactory.CreateDbContextAsync();

                // Load all games for the year
                var games = await context.Games
                    .Where(g => g.Year == targetYear)
                    .OrderBy(g => g.Week)
                    .ToListAsync();

                if (games.Count == 0)
                    return Ok(new List<object>());

                // Load team metadata keyed by TeamID for conference/tier lookups
                var teamIds = games.SelectMany(g => new[] { g.WinnerId, g.LoserId }).Distinct().ToList();
                var teams = await context.Teams
                    .Where(t => teamIds.Contains(t.TeamID))
                    .ToDictionaryAsync(t => t.TeamID);

                // Pre-game TeamRecords: for projection we use season-to-date stats
                // accumulated BEFORE each game (i.e., from the prior year for week 1,
                // or running totals from the same season up to but not including that week).
                // We approximate by loading the full-season record and using the existing
                // prediction service which is designed for pre-game use.
                var teamRecords = await context.TeamRecords
                    .Where(tr => tr.Year == targetYear)
                    .ToDictionaryAsync(tr => tr.TeamID);

                // Also load prior-year records as fallback for early-season games
                var priorYearRecords = await context.TeamRecords
                    .Where(tr => tr.Year == targetYear - 1)
                    .ToDictionaryAsync(tr => tr.TeamID);

                var avgScoreDeltas = await context.AvgScoreDeltas.ToListAsync();

                // Compute league average from all games this season
                var avgTeamScore = games.Count > 0
                    ? (games.Average(g => g.WPoints) + games.Average(g => g.LPoints)) / 2.0
                    : 28.0;

                var results = games.Select(g =>
                {
                    teams.TryGetValue(g.WinnerId, out var winner);
                    teams.TryGetValue(g.LoserId, out var loser);

                    var winnerConf = winner?.ConferenceAbbr ?? winner?.Conference ?? "";
                    var loserConf  = loser?.ConferenceAbbr  ?? loser?.Conference  ?? "";
                    var winnerTier = GetConferenceTier(winner?.Conference, winner?.TeamName);
                    var loserTier  = GetConferenceTier(loser?.Conference,  loser?.TeamName);

                    // Projected scores: use pre-season (or prior year) records so the
                    // projection reflects what was knowable before the game was played.
                    double? projWinner = null, projLoser = null;
                    try
                    {
                        // Pick up the running record for this team (full-season approximation)
                        var wrec = teamRecords.GetValueOrDefault(g.WinnerId)
                                   ?? priorYearRecords.GetValueOrDefault(g.WinnerId);
                        var lrec = teamRecords.GetValueOrDefault(g.LoserId)
                                   ?? priorYearRecords.GetValueOrDefault(g.LoserId);

                        if (wrec != null && lrec != null)
                        {
                            var wGames = wrec.Wins + wrec.Losses;
                            var lGames = lrec.Wins + lrec.Losses;
                            var wWinPct = wGames > 0
                                ? Math.Round((decimal)wrec.Wins / wGames * 20m, MidpointRounding.AwayFromZero) / 20m
                                : 0m;
                            var lWinPct = lGames > 0
                                ? Math.Round((decimal)lrec.Wins / lGames * 20m, MidpointRounding.AwayFromZero) / 20m
                                : 0m;

                            var maxPct = Math.Max(wWinPct, lWinPct);
                            var minPct = Math.Min(wWinPct, lWinPct);
                            var asd = avgScoreDeltas.FirstOrDefault(
                                a => a.Team1WinPct == maxPct && a.Team2WinPct == minPct);
                            var delta = asd != null && asd.SampleSize >= 10
                                ? Math.Max(-35.0, Math.Min(35.0, (double)asd.AverageScoreDelta))
                                : 7.0;

                            // Winner perspective
                            var deltaFromWinner = wWinPct >= lWinPct ? delta : -delta;

                            // Home field: 'W' = winner is home, 'L' = loser is home, 'N' = neutral
                            var hfa = g.Location == 'W' ? 2.5 : g.Location == 'L' ? -2.5 : 0.0;
                            deltaFromWinner += hfa;

                            // Power rating adjustment
                            if (wrec.Ranking.HasValue && lrec.Ranking.HasValue)
                            {
                                var ratingDiff = (double)(wrec.Ranking.Value - lrec.Ranking.Value);
                                deltaFromWinner += ratingDiff * 0.15;
                            }

                            projWinner = Math.Round(avgTeamScore + deltaFromWinner / 2.0, 1);
                            projLoser  = Math.Round(avgTeamScore - deltaFromWinner / 2.0, 1);
                        }
                    }
                    catch { /* projection unavailable */ }

                    var actualOU  = g.WPoints + g.LPoints;
                    var projOU    = projWinner.HasValue && projLoser.HasValue
                                    ? Math.Round(projWinner.Value + projLoser.Value, 1)
                                    : (double?)null;

                    return new
                    {
                        g.Id,
                        g.Year,
                        g.Week,
                        WinnerName     = g.WinnerName,
                        WinnerId       = g.WinnerId,
                        WinnerConf     = winnerConf,
                        WinnerTier     = winnerTier,
                        WPoints        = g.WPoints,
                        LoserName      = g.LoserName,
                        LoserId        = g.LoserId,
                        LoserConf      = loserConf,
                        LoserTier      = loserTier,
                        LPoints        = g.LPoints,
                        g.Location,
                        ActualOU       = actualOU,
                        ProjWinnerScore = projWinner,
                        ProjLoserScore  = projLoser,
                        ProjOU         = projOU
                    };
                }).ToList();

                return Ok(results);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving schedule");
                return StatusCode(500, "An error occurred while retrieving the schedule.");
            }
        }

        /// <summary>
        /// Determines the conference tier for rankings.
        /// P4 = Power 4 conferences (SEC, Big Ten, Big 12, ACC)
        /// G5 = Group of 5 conferences (American, Mountain West, Sun Belt, MAC, C-USA)
        /// Independent = FBS independents (Army, Liberty, etc.)
        /// Team-name overrides handle edge cases where conference data doesn't reflect competitive tier.
        /// </summary>
        private static string GetConferenceTier(string? conference, string? teamName = null)
        {
            // Team-name overrides for independents whose tier doesn't match their conference string
            if (!string.IsNullOrEmpty(teamName))
            {
                if (teamName.Equals("Notre Dame", StringComparison.OrdinalIgnoreCase))
                    return "P4";
                if (teamName.Equals("Connecticut", StringComparison.OrdinalIgnoreCase))
                    return "G5";
            }

            if (string.IsNullOrEmpty(conference))
                return "Other";

            // Power 4 conferences
            var power4 = new[] { "SEC", "Big Ten", "Big 12", "ACC" };
            if (power4.Any(p4 => conference.Contains(p4, StringComparison.OrdinalIgnoreCase)))
                return "P4";

            // Group of 5 conferences
            var group5 = new[]
            {
                "American Athletic", "American", "AAC",
                "Mountain West",
                "Sun Belt",
                "Mid-American", "MAC",
                "Conference USA", "C-USA",
                "Pac-12"
            };
            if (group5.Any(g5 => conference.Contains(g5, StringComparison.OrdinalIgnoreCase)))
                return "G5";

            // Independent teams (Army, Liberty, etc.)
            if (conference.Contains("Independent", StringComparison.OrdinalIgnoreCase))
                return "Independent";

            return "Other";
        }
    }

    /// <summary>
    /// Request model for batch matchup predictions.
    /// </summary>
    public class MatchupBatchRequest
    {
        public int Year { get; set; }
        public List<NCAA_Power_Ratings.Services.MatchupRequest> Matchups { get; set; } = new();
    }
}
