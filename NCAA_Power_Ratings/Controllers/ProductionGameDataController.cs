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
