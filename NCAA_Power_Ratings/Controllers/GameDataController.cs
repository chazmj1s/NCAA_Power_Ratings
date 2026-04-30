using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCAA_Power_Ratings.Configuration;
using NCAA_Power_Ratings.Models;
using NCAA_Power_Ratings.Interfaces;
using NCAA_Power_Ratings.Services;
using NCAA_Power_Ratings.Data;
using NCAA_Power_Ratings.Utilities;

namespace NCAA_Power_Ratings.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameDataController(
        IGameDataService gameDataService, 
        TeamMetricsService teamMetrics,
        IDbContextFactory<NCAAContext> contextFactory,
        ScoreDeltaCalculator scoreDeltaCalculator,
        IOptions<MetricsConfiguration> config,
        ILogger<GameDataController> logger) : ControllerBase
    {
        private readonly MetricsConfiguration _config = config.Value;

        /// <summary>
        /// Extract game data starting from the provided year up through the current year.
        /// Example: GET /api/gamedata/initialGamesExtract?startYear=2020
        /// </summary>
        [HttpGet("initialGamesExtract")]
        public async Task<ActionResult<List<Game>>> InitialGamesExtract([FromQuery] int? startYear)
        {
            try
            {
                var result = await gameDataService.ExtractGameDataHistoryAsync(startYear);
                return Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error extracting game data for startYear={StartYear}", startYear);
                return StatusCode(500, "An error occurred while extracting game data.");
            }
        }
        
        /// <summary>
         /// Extract load game data for last 60 years from text files.
         /// Example: GET /api/gamedata/initialGamesExtract?startYear=2020
         /// </summary>
        [HttpGet("loadGameHistoryFromFiles")]
        public async Task<ActionResult<List<Game>>> LoadGameHistoryFromFiles()
        {
            try
            {
                var result = await gameDataService.LoadGameHistoryFromFiles();
                return Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading game data from files");
                return StatusCode(500, "An error occurred while extracting game data.");
            }
        }

        /// <summary>
        /// Updates team records for the specified year and redirects to the home page.
        /// Example: POST /api/gamedata/updateRecords?year=2021
        /// </summary>
        [HttpPost("updateTeamRecords")]
        public async Task<IActionResult> UpdateTeamRecords([FromQuery] int? year)
        {
            try
            {
                // Call UpdateTeamRecordsAsync on the injected service
                await gameDataService.UpdateTeamRecordsAsync(year);
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating team records for year={Year}", year);
                return StatusCode(500, "An error occurred while updating team records.");
            }
        }

        /// <summary>
        /// Updates game data for a specific year and week by fetching fresh data from the web.
        /// Adds new games and updates existing games for the specified week.
        /// Example: POST /api/gamedata/updateWeekGames?year=2024&week=10
        /// </summary>
        [HttpPost("updateWeekGames")]
        public async Task<IActionResult> UpdateWeekGames([FromQuery] int year, [FromQuery] int week)
        {
            try
            {
                var gamesProcessed = await gameDataService.UpdateGameDataForYearAndWeekAsync(year, week);
                await teamMetrics.SetSOS(year, week);  // auto-recalc SOS
                await teamMetrics.CalculatePowerRatings(year);         // refresh power ratings


                return Ok(new 
                { 
                    message = $"Successfully processed games for {year} week {week}",
                    gamesProcessed = gamesProcessed,
                    year = year,
                    week = week
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating game data for year={Year}, week={Week}", year, week);
                return StatusCode(500, "An error occurred while updating game data.");
            }
        }

        /// <summary>
        /// Calculates team performance trends based on the last 10 years of data.
        /// Returns normalized win percentages, weighted averages, and projected wins as JSON.
        /// Example: GET /api/gamedata/calculateTrends
        /// </summary>
        [HttpGet("calculateTrends")]
        public async Task<IActionResult> CalculateTrends()
        {
            try
            {
                var result = await teamMetrics.CalculateTrend();
                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error calculating team trends");
                return StatusCode(500, "An error occurred while calculating team trends.");
            }
        }

        /// <summary>
        /// Calculates and sets Strength of Schedule (SOS) values for all teams in the specified year.
        /// Computes BaseSOS, SubSOS, and CombinedSOS metrics.
        /// Example: POST /api/gamedata/setSOS?year=2024
        /// </summary>
        [HttpPost("setSOS")]
        public async Task<IActionResult> SetSOS([FromQuery] int? year, [FromQuery] int? week)
        {
            try
            {
                await teamMetrics.SetSOS(year, week);
                return Ok(new { message = $"SOS values calculated successfully for year {year ?? DateTime.Now.Year} and week { week ?? 0}\r\n" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error calculating SOS for year={Year}", year);
                return StatusCode(500, "An error occurred while calculating SOS.");
            }
        }

        /// <summary>
        /// Calculates power ratings for a specific year or current year if not specified.
        /// Example: GET /api/gamedata/calculatePowerRatings?year=2024
        /// </summary>
        [HttpGet("calculatePowerRatings")]
        public async Task<IActionResult> CalculatePowerRatings([FromQuery] int? year)
        {
            try
            {
                await teamMetrics.CalculatePowerRatings(year);
                return Ok(new { message = $"Power ratings calculated for {year ?? DateTime.Now.Year}" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error calculating power ratings");
                return StatusCode(500, "An error occurred while calculating power ratings.");
            }
        }

        /// <summary>
        /// Backfills power ratings for all years in the TeamRecords table.
        /// Optionally specify a start year to process only from that year forward.
        /// Example: POST /api/gamedata/backfillPowerRatings
        /// Example: POST /api/gamedata/backfillPowerRatings?startYear=2000
        /// </summary>
        [HttpPost("backfillPowerRatings")]
        public async Task<IActionResult> BackfillPowerRatings([FromQuery] int? startYear)
        {
            try
            {
                await using var context = await contextFactory.CreateDbContextAsync();

                // Get distinct years from TeamRecords
                var years = await context.TeamRecords
                    .Select(tr => tr.Year)
                    .Distinct()
                    .OrderBy(y => y)
                    .ToListAsync();

                // Filter by start year if provided
                if (startYear.HasValue)
                {
                    years = years.Where(y => y >= startYear.Value).ToList();
                }

                var results = new List<object>();
                var successCount = 0;
                var failCount = 0;

                logger.LogInformation("Starting power rating backfill for {YearCount} years", years.Count);

                foreach (var year in years)
                {
                    try
                    {
                        await teamMetrics.CalculatePowerRatings(year);
                        results.Add(new { year, status = "success" });
                        successCount++;
                        logger.LogInformation("Power ratings calculated for year {Year}", year);
                    }
                    catch (Exception ex)
                    {
                        results.Add(new { year, status = "failed", error = ex.Message });
                        failCount++;
                        logger.LogError(ex, "Failed to calculate power ratings for year {Year}", year);
                    }
                }

                return Ok(new 
                { 
                    message = $"Power rating backfill complete: {successCount} succeeded, {failCount} failed",
                    totalYears = years.Count,
                    successCount,
                    failCount,
                    startYear = years.FirstOrDefault(),
                    endYear = years.LastOrDefault(),
                    results
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during power rating backfill");
                return StatusCode(500, "An error occurred while backfilling power ratings.");
            }
        }

        /// <summary>
        /// Backfills SOS and Power Ratings for all years in the TeamRecords table.
        /// This runs both SetSOS (with week 0 to use projected wins) and CalculatePowerRatings for each year.
        /// Optionally specify a start year to process only from that year forward.
        /// Example: POST /api/gamedata/backfillAllMetrics
        /// Example: POST /api/gamedata/backfillAllMetrics?startYear=2000
        /// </summary>
        [HttpPost("backfillAllMetrics")]
        public async Task<IActionResult> BackfillAllMetrics([FromQuery] int? startYear)
        {
            try
            {
                await using var context = await contextFactory.CreateDbContextAsync();

                // Get distinct years from TeamRecords
                var years = await context.TeamRecords
                    .Select(tr => tr.Year)
                    .Distinct()
                    .OrderBy(y => y)
                    .ToListAsync();

                // Filter by start year if provided
                if (startYear.HasValue)
                {
                    years = years.Where(y => y >= startYear.Value).ToList();
                }

                var results = new List<object>();
                var successCount = 0;
                var failCount = 0;

                logger.LogInformation("Starting SOS and power rating backfill for {YearCount} years", years.Count);

                foreach (var year in years)
                {
                    try
                    {
                        // First set SOS with week 0 (uses projected wins)
                        await teamMetrics.SetSOS(year, week: 0);
                        // Then calculate power ratings
                        await teamMetrics.CalculatePowerRatings(year);

                        results.Add(new { year, status = "success" });
                        successCount++;
                        logger.LogInformation("SOS and power ratings calculated for year {Year}", year);
                    }
                    catch (Exception ex)
                    {
                        results.Add(new { year, status = "failed", error = ex.Message });
                        failCount++;
                        logger.LogError(ex, "Failed to calculate metrics for year {Year}", year);
                    }
                }

                return Ok(new 
                { 
                    message = $"Metrics backfill complete: {successCount} succeeded, {failCount} failed",
                    totalYears = years.Count,
                    successCount,
                    failCount,
                    startYear = years.FirstOrDefault(),
                    endYear = years.LastOrDefault(),
                    results
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during metrics backfill");
                return StatusCode(500, "An error occurred while backfilling metrics.");
            }
        }

        /// <summary>
        /// Processes a single file from the NCAA Raw Game Data directory for debugging.
        /// Example: POST /api/gamedata/processSingleFile?filePath=D:\NCAA Raw Game Data\2024.txt
        /// </summary>
        [HttpPost("processSingleFile")]
        public async Task<IActionResult> ProcessSingleFile([FromQuery] string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return BadRequest("File path is required.");
                }

                var recordsProcessed = await gameDataService.ProcessSingleFileAsync(filePath);

                return Ok(new 
                { 
                    message = $"Successfully processed file: {Path.GetFileName(filePath)}",
                    recordsProcessed = recordsProcessed,
                    filePath = filePath
                });
            }
            catch (FileNotFoundException ex)
            {
                logger.LogError(ex, "File not found: {FilePath}", filePath);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing file: {FilePath}", filePath);
                return StatusCode(500, "An error occurred while processing the file.");
            }
        }

        /// <summary>
        /// Updates game data for a specific year and week from a local file instead of web scraping.
        /// Automatically constructs the filename as {year}.txt from the NCAA Raw Game Data directory.
        /// Useful for debugging when the website is inaccessible or for testing with known data.
        /// Example: POST /api/gamedata/updateWeekGamesFromFile?year=2024&week=10
        /// </summary>
        [HttpPost("updateWeekGamesFromFile")]
        public async Task<IActionResult> UpdateWeekGamesFromFile([FromQuery] int year, [FromQuery] int week)
        {
            try
            {
                // Construct filename from year (e.g., 2024.txt)
                var fileName = $"{year}.txt";

                // Construct relative path to NCAA Raw Game Data directory
                var dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), "NCAA Raw Game Data");
                var filePath = Path.Combine(dataDirectory, fileName);

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound($"File '{fileName}' not found in NCAA Raw Game Data directory.");
                }

                var gamesProcessed = await gameDataService.UpdateGameDataFromFileAsync(filePath, year, week);
                await teamMetrics.SetSOS(year, week);  // auto-recalc SOS
                await teamMetrics.CalculatePowerRatings(year);         // refresh power ratings

                return Ok(new 
                { 
                    message = $"Successfully processed games for {year} week {week} from file",
                    gamesProcessed = gamesProcessed,
                    year = year,
                    week = week,
                    sourceFile = fileName
                });
            }
            catch (FileNotFoundException ex)
            {
                logger.LogError(ex, "File not found: {Year}.txt", year);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating game data from file: {Year}.txt, year={Year}, week={Week}", year, year, week);
                return StatusCode(500, "An error occurred while updating game data from file.");
            }
        }

        /// <summary>
        /// Lists available game data files in the NCAA Raw Game Data directory.
        /// Example: GET /api/gamedata/listAvailableFiles
        /// </summary>
        [HttpGet("listAvailableFiles")]
        public IActionResult ListAvailableFiles()
        {
            try
            {
                var dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), "NCAA Raw Game Data");

                if (!Directory.Exists(dataDirectory))
                {
                    return NotFound("NCAA Raw Game Data directory not found.");
                }

                var files = Directory.GetFiles(dataDirectory, "*.txt")
                    .Select(f => Path.GetFileName(f))
                    .OrderBy(f => f)
                    .ToList();

                return Ok(new 
                { 
                    directory = dataDirectory,
                    fileCount = files.Count,
                    files = files
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error listing available files");
                return StatusCode(500, "An error occurred while listing available files.");
            }
        }

        /// <summary>
        /// Query team records with filters for wins, losses, year range, and PowerRating range.
        /// Example: GET /api/gamedata/queryTeamRecords?wins=13&losses=3
        /// Example: GET /api/gamedata/queryTeamRecords?minPowerRating=-0.02&maxPowerRating=0.01
        /// Example: GET /api/gamedata/queryTeamRecords?startYear=2020&endYear=2024&minWins=10
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
        /// Analytics endpoint that provides insights into the relationship between actual results and calculated metrics.
        /// Includes: overperformers/underperformers, correlations, extremes, and distributions.
        /// Example: GET /api/gamedata/analytics?startYear=2020&endYear=2024
        /// </summary>
        [HttpGet("analytics")]
        public async Task<IActionResult> GetAnalytics(
            [FromQuery] int? startYear,
            [FromQuery] int? endYear)
        {
            try
            {
                await using var context = await contextFactory.CreateDbContextAsync();

                var query = context.TeamRecords
                    .Include(tr => tr.Team)
                    .Where(tr => tr.PowerRating != null);

                if (startYear.HasValue)
                    query = query.Where(tr => tr.Year >= startYear.Value);

                if (endYear.HasValue)
                    query = query.Where(tr => tr.Year <= endYear.Value);

                var allData = await query
                    .Select(tr => new
                    {
                        tr.Year,
                        TeamName = tr.Team!.TeamName,
                        tr.Wins,
                        tr.Losses,
                        TotalGames = tr.Wins + tr.Losses,
                        WinPct = (tr.Wins + tr.Losses) > 0 ? (double)tr.Wins / (tr.Wins + tr.Losses) : 0,
                        tr.PointsFor,
                        tr.PointsAgainst,
                        PointDiff = tr.PointsFor - tr.PointsAgainst,
                        tr.BaseSOS,
                        tr.SubSOS,
                        tr.CombinedSOS,
                        tr.PowerRating
                    })
                    .ToListAsync();

                // Calculate expected wins based on PowerRating for comparison
                var teamsWithMetrics = allData.Select(t => new
                {
                    t.Year,
                    t.TeamName,
                    t.Wins,
                    t.Losses,
                    t.TotalGames,
                    t.WinPct,
                    ActualWins = (double)t.Wins,
                    // Rough estimate: normalize PowerRating to win percentage (this is a simplification)
                    ExpectedWinPct = 0.5 + ((double)(t.PowerRating ?? 0) * 0.15), // Scale PowerRating to win%
                    ExpectedWins = t.TotalGames * (0.5 + ((double)(t.PowerRating ?? 0) * 0.15)),
                    WinDifference = t.Wins - (t.TotalGames * (0.5 + ((double)(t.PowerRating ?? 0) * 0.15))),
                    t.PointDiff,
                    t.BaseSOS,
                    t.SubSOS,
                    t.CombinedSOS,
                    t.PowerRating
                }).ToList();

                // Top 10 Overperformers (won more than PowerRating suggested)
                var overperformers = teamsWithMetrics
                    .Where(t => t.Wins >= 8) // Only teams with meaningful sample
                    .OrderByDescending(t => t.WinDifference)
                    .Take(10)
                    .Select(t => new
                    {
                        t.Year,
                        t.TeamName,
                        ActualRecord = $"{t.Wins}-{t.Losses}",
                        t.ActualWins,
                        ExpectedWins = Math.Round(t.ExpectedWins, 1),
                        WinDifference = Math.Round(t.WinDifference, 1),
                        t.PowerRating,
                        t.CombinedSOS
                    })
                    .ToList();

                // Top 10 Underperformers (won less than PowerRating suggested)
                var underperformers = teamsWithMetrics
                    .Where(t => t.Wins >= 8) // Only teams with meaningful sample
                    .OrderBy(t => t.WinDifference)
                    .Take(10)
                    .Select(t => new
                    {
                        t.Year,
                        t.TeamName,
                        ActualRecord = $"{t.Wins}-{t.Losses}",
                        t.ActualWins,
                        ExpectedWins = Math.Round(t.ExpectedWins, 1),
                        WinDifference = Math.Round(t.WinDifference, 1),
                        t.PowerRating,
                        t.CombinedSOS
                    })
                    .ToList();

                // Top 10 PowerRating teams
                var topPowerRatings = teamsWithMetrics
                    .OrderByDescending(t => t.PowerRating)
                    .Take(10)
                    .Select(t => new
                    {
                        t.Year,
                        t.TeamName,
                        Record = $"{t.Wins}-{t.Losses}",
                        t.PowerRating,
                        t.CombinedSOS,
                        t.PointDiff
                    })
                    .ToList();

                // Bottom 10 PowerRating teams
                var bottomPowerRatings = teamsWithMetrics
                    .OrderBy(t => t.PowerRating)
                    .Take(10)
                    .Select(t => new
                    {
                        t.Year,
                        t.TeamName,
                        Record = $"{t.Wins}-{t.Losses}",
                        t.PowerRating,
                        t.CombinedSOS,
                        t.PointDiff
                    })
                    .ToList();

                // Distribution by win totals
                var distributionByWins = teamsWithMetrics
                    .GroupBy(t => t.Wins)
                    .Select(g => new
                    {
                        Wins = g.Key,
                        TeamCount = g.Count(),
                        AvgPowerRating = Math.Round(g.Average(t => (double)(t.PowerRating ?? 0)), 4),
                        AvgSOS = Math.Round(g.Average(t => (double)(t.CombinedSOS ?? 1)), 4),
                        AvgPointDiff = Math.Round(g.Average(t => t.PointDiff), 1)
                    })
                    .OrderBy(x => x.Wins)
                    .ToList();

                // Interesting anomalies: Great records with negative PowerRating
                var luckyTeams = teamsWithMetrics
                    .Where(t => t.Wins >= 10 && t.PowerRating < 0)
                    .OrderBy(t => t.PowerRating)
                    .Select(t => new
                    {
                        t.Year,
                        t.TeamName,
                        Record = $"{t.Wins}-{t.Losses}",
                        t.PowerRating,
                        t.CombinedSOS,
                        Description = "Won 10+ games despite negative PowerRating"
                    })
                    .ToList();

                // Unlucky teams: Losing records with positive PowerRating
                var unluckyTeams = teamsWithMetrics
                    .Where(t => t.Wins < t.Losses && t.PowerRating > 0)
                    .OrderByDescending(t => t.PowerRating)
                    .Take(10)
                    .Select(t => new
                    {
                        t.Year,
                        t.TeamName,
                        Record = $"{t.Wins}-{t.Losses}",
                        t.PowerRating,
                        t.CombinedSOS,
                        Description = "Losing record despite positive PowerRating"
                    })
                    .ToList();

                // Correlation stats
                var correlation = CalculateCorrelation(
                    teamsWithMetrics.Select(t => t.ActualWins).ToList(),
                    teamsWithMetrics.Select(t => (double)(t.PowerRating ?? 0)).ToList()
                );

                var sosCorrelation = CalculateCorrelation(
                    teamsWithMetrics.Select(t => t.ActualWins).ToList(),
                    teamsWithMetrics.Select(t => (double)(t.CombinedSOS ?? 1)).ToList()
                );

                return Ok(new
                {
                    summary = new
                    {
                        totalTeams = allData.Count,
                        yearRange = $"{allData.Min(t => t.Year)}-{allData.Max(t => t.Year)}",
                        avgPowerRating = Math.Round(allData.Average(t => (double)(t.PowerRating ?? 0)), 4),
                        avgSOS = Math.Round(allData.Average(t => (double)(t.CombinedSOS ?? 1)), 4),
                        correlationWinsToPowerRating = Math.Round(correlation, 3),
                        correlationWinsToSOS = Math.Round(sosCorrelation, 3)
                    },
                    overperformers,
                    underperformers,
                    topPowerRatings,
                    bottomPowerRatings,
                    luckyTeams,
                    unluckyTeams,
                    distributionByWins
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error generating analytics");
                return StatusCode(500, "An error occurred while generating analytics.");
            }
        }

        /// <summary>
        /// Helper method to calculate Pearson correlation coefficient
        /// </summary>
        private double CalculateCorrelation(List<double> x, List<double> y)
        {
            if (x.Count != y.Count || x.Count == 0)
                return 0;

            var n = x.Count;
            var sumX = x.Sum();
            var sumY = y.Sum();
            var sumXY = x.Zip(y, (a, b) => a * b).Sum();
            var sumX2 = x.Sum(a => a * a);
            var sumY2 = y.Sum(b => b * b);

            var numerator = (n * sumXY) - (sumX * sumY);
            var denominator = Math.Sqrt(((n * sumX2) - (sumX * sumX)) * ((n * sumY2) - (sumY * sumY)));

            if (denominator == 0)
                return 0;

            return numerator / denominator;
        }

        /// <summary>
        /// Recalculates the AvgScoreDeltas table from scratch using all historical game data.
        /// This is needed after fixing the AverageScoreDelta data type bug.
        /// Example: POST /api/gamedata/recalculateScoreDeltas
        /// </summary>
        [HttpPost("recalculateScoreDeltas")]
        public async Task<IActionResult> RecalculateScoreDeltas()
        {
            try
            {
                logger.LogInformation("Starting AvgScoreDeltas recalculation...");

                var results = await scoreDeltaCalculator.CalculateAvgScoreDeltasAsync();

                await using var context = await contextFactory.CreateDbContextAsync();

                // Clear existing data
                await context.Database.ExecuteSqlRawAsync("DELETE FROM AvgScoreDeltas");

                // Insert new calculated data
                foreach (var stat in results)
                {
                    var entity = new AvgScoreDelta
                    {
                        Team1Wins = stat.Team1Wins,
                        Team2Wins = stat.Team2Wins,
                        AverageScoreDelta = (decimal)stat.AvgDelta,
                        StDevP = (decimal)stat.StdDevP,
                        SampleSize = stat.SampleSize
                    };
                    context.AvgScoreDeltas.Add(entity);
                }

                await context.SaveChangesAsync();

                logger.LogInformation("AvgScoreDeltas recalculation complete: {Count} records", results.Count);

                return Ok(new
                {
                    message = "AvgScoreDeltas recalculated successfully",
                    recordCount = results.Count,
                    sample = results.Take(5).Select(r => new
                    {
                        r.Team1Wins,
                        r.Team2Wins,
                        AverageScoreDelta = r.AvgDelta,
                        StDevP = r.StdDevP,
                        r.SampleSize
                    })
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error recalculating AvgScoreDeltas");
                return StatusCode(500, "An error occurred while recalculating score deltas.");
            }
        }

        /// <summary>
        /// Recreates the AvgScoreDeltas table with correct REAL data type for negative values.
        /// This fixes the SQLite schema issue where decimals were being stored as integers.
        /// WARNING: This will delete all existing data in the table.
        /// Example: POST /api/gamedata/recreateAvgScoreDeltasTable
        /// </summary>
        [HttpPost("recreateAvgScoreDeltasTable")]
        public async Task<IActionResult> RecreateAvgScoreDeltasTable()
        {
            try
            {
                logger.LogInformation("Recreating AvgScoreDeltas table with correct schema...");

                await using var context = await contextFactory.CreateDbContextAsync();

                // Drop and recreate table with correct data type
                await context.Database.ExecuteSqlRawAsync(@"
                    DROP TABLE IF EXISTS AvgScoreDeltas_Backup;
                    ALTER TABLE AvgScoreDeltas RENAME TO AvgScoreDeltas_Backup;

                    CREATE TABLE AvgScoreDeltas (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Team1Wins INTEGER NOT NULL,
                        Team2Wins INTEGER NOT NULL,
                        AverageScoreDelta REAL NOT NULL,
                        StDevP REAL NOT NULL,
                        SampleSize INTEGER
                    );
                ");

                logger.LogInformation("AvgScoreDeltas table recreated successfully");

                return Ok(new
                {
                    message = "AvgScoreDeltas table recreated with REAL data type",
                    nextStep = "Call POST /api/gamedata/recalculateScoreDeltas to populate with correct data"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error recreating AvgScoreDeltas table");
                return StatusCode(500, "An error occurred while recreating the table.");
            }
        }

        /// <summary>
        /// Shows detailed game-by-game analysis for a specific team including Z-scores.
        /// Example: GET /api/gamedata/analyzeTeamGames?teamId=110&year=2024
        /// </summary>
        [HttpGet("analyzeTeamGames")]
        public async Task<IActionResult> AnalyzeTeamGames([FromQuery] int teamId, [FromQuery] int? year)
        {
            try
            {
                await using var context = await contextFactory.CreateDbContextAsync();

                var targetYear = year ?? DateTime.Now.Year;

                // Get team's games
                var gamesFromWinner = context.Games
                    .Where(g => g.Year == targetYear && g.WinnerId == teamId)
                    .Select(g => new {
                        g.Week,
                        TeamId = g.WinnerId,
                        TeamName = g.WinnerName,
                        OpponentId = g.LoserId,
                        OpponentName = g.LoserName,
                        TeamPoints = g.WPoints,
                        OpponentPoints = g.LPoints,
                        Delta = g.WPoints - g.LPoints,
                        Result = "W",
                        g.Location,
                        IsHomeTeam = g.Location == 'W',
                        LocationDisplay = g.Location == 'W' ? "Home" : g.Location == 'L' ? "Away" : "Neutral"
                    });

                var gamesFromLoser = context.Games
                    .Where(g => g.Year == targetYear && g.LoserId == teamId)
                    .Select(g => new {
                        g.Week,
                        TeamId = g.LoserId,
                        TeamName = g.LoserName,
                        OpponentId = g.WinnerId,
                        OpponentName = g.WinnerName,
                        TeamPoints = g.LPoints,
                        OpponentPoints = g.WPoints,
                        Delta = g.LPoints - g.WPoints,
                        Result = "L",
                        g.Location,
                        IsHomeTeam = g.Location == 'L',
                        LocationDisplay = g.Location == 'L' ? "Home" : g.Location == 'W' ? "Away" : "Neutral"
                    });

                var games = await gamesFromWinner.Union(gamesFromLoser)
                    .OrderBy(g => g.Week)
                    .ToListAsync();

                // Get wins lookup
                var winsLookup = await context.TeamRecords
                    .Where(tr => tr.Year == targetYear)
                    .ToDictionaryAsync(tr => tr.TeamID, tr => (int)tr.Wins);

                // Get AvgScoreDeltas
                var avgScoreDeltas = await context.AvgScoreDeltas.ToListAsync();

                // Calculate Z-scores
                var homeFieldAdvantage = _config.HomeFieldAdvantage;
                var analysis = games.Select(g => {
                    var teamWins = winsLookup.GetValueOrDefault(g.TeamId, 0);
                    var oppWins = winsLookup.GetValueOrDefault(g.OpponentId, 0);
                    var maxWins = Math.Max(teamWins, oppWins);
                    var minWins = Math.Min(teamWins, oppWins);

                    var delta = g.Delta;

                    var asd = avgScoreDeltas.FirstOrDefault(
                        a => a.Team1Wins == maxWins && a.Team2Wins == minWins);

                    double zScore = 0.0;
                    double expectedDelta = 0.0;
                    double homeAdjustment = 0.0;

                    if (asd != null && asd.StDevP != 0)
                    {
                        // Expected delta is from higher-win team's perspective
                        expectedDelta = (double)asd.AverageScoreDelta;

                        // Adjust expected to team's perspective
                        var expectedFromTeamPerspective = teamWins >= oppWins 
                            ? expectedDelta 
                            : -expectedDelta;

                        // Adjust for home field advantage
                        if (g.IsHomeTeam)
                        {
                            expectedFromTeamPerspective += homeFieldAdvantage;
                            homeAdjustment = homeFieldAdvantage;
                        }
                        else if (g.Location != 'N') // opponent is home
                        {
                            expectedFromTeamPerspective -= homeFieldAdvantage;
                            homeAdjustment = -homeFieldAdvantage;
                        }
                        // else: neutral site, no adjustment

                        // Z-score calculation
                        zScore = (delta - expectedFromTeamPerspective) / (double)asd.StDevP;
                    }

                    var baseExpected = teamWins >= oppWins ? expectedDelta : -expectedDelta;
                    var adjustedExpected = baseExpected + homeAdjustment;

                    return new {
                        g.Week,
                        g.OpponentName,
                        Location = g.LocationDisplay,
                        g.Result,
                        g.Delta,
                        TeamFinalWins = teamWins,
                        OppFinalWins = oppWins,
                        BaseExpectedDelta = Math.Round(baseExpected, 1),
                        HomeAdjustment = Math.Round(homeAdjustment, 1),
                        AdjustedExpectedDelta = Math.Round(adjustedExpected, 1),
                        ActualDelta = delta,
                        Difference = Math.Round(delta - adjustedExpected, 1),
                        ZScore = Math.Round(zScore, 3),
                        Performance = zScore > _config.DominantPerformanceThreshold ? "Dominant" 
                                    : zScore > _config.UnderperformedThreshold ? "Expected" 
                                    : "Underperformed"
                    };
                }).ToList();

                var avgZScore = analysis.Average(a => a.ZScore);
                var teamRecord = await context.TeamRecords
                    .Where(tr => tr.TeamID == teamId && tr.Year == targetYear)
                    .FirstOrDefaultAsync();

                return Ok(new {
                    teamId,
                    year = targetYear,
                    record = $"{teamRecord?.Wins}-{teamRecord?.Losses}",
                    combinedSOS = teamRecord?.CombinedSOS,
                    avgZScore = Math.Round(avgZScore, 4),
                    powerRating = teamRecord?.PowerRating,
                    calculatedPowerRating = Math.Round(avgZScore * (double)(teamRecord?.CombinedSOS ?? 1.0m), 4),
                    games = analysis
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error analyzing team games");
                return StatusCode(500, "An error occurred during analysis.");
            }
        }

        /// <summary>
        /// Diagnostic endpoint to show raw game delta calculations for debugging.
        /// Shows sample games including upsets to verify negative deltas are calculated.
        /// Example: GET /api/gamedata/diagnosticScoreDeltas?year=2024&limit=50
        /// </summary>
        [HttpGet("diagnosticScoreDeltas")]
        public async Task<IActionResult> DiagnosticScoreDeltas([FromQuery] int? year, [FromQuery] int limit = 50)
        {
            try
            {
                await using var context = await contextFactory.CreateDbContextAsync();

                var targetYear = year ?? 2024;

                // Get raw game data with team records
                var gameData = await context.Games
                    .Where(g => g.Year == targetYear)
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
                            WinnerId = x.Game.WinnerId,
                            WinnerName = x.Game.WinnerName,
                            WinnerWins = x.WinnerRecord.Wins,
                            WinnerPoints = x.Game.WPoints,
                            LoserId = x.Game.LoserId,
                            LoserName = x.Game.LoserName,
                            LoserWins = tl.Wins,
                            LoserPoints = x.Game.LPoints
                        }
                    )
                    .Take(limit)
                    .ToListAsync();

                var results = gameData.Select(x => new
                {
                    x.WinnerName,
                    x.WinnerWins,
                    x.WinnerPoints,
                    x.LoserName,
                    x.LoserWins,
                    x.LoserPoints,
                    IsUpset = x.WinnerWins < x.LoserWins,
                    // Apply same logic as ScoreDeltaCalculator
                    Team1Wins = x.WinnerWins >= x.LoserWins ? x.WinnerWins : x.LoserWins,
                    Team2Wins = x.WinnerWins >= x.LoserWins ? x.LoserWins : x.WinnerWins,
                    Delta = x.WinnerWins >= x.LoserWins
                        ? x.WinnerPoints - x.LoserPoints
                        : x.LoserPoints - x.WinnerPoints,
                    Explanation = x.WinnerWins >= x.LoserWins
                        ? $"Normal: {x.WinnerWins}-win team beat {x.LoserWins}-win team by {x.WinnerPoints - x.LoserPoints}"
                        : $"UPSET: {x.WinnerWins}-win team beat {x.LoserWins}-win team, delta from {x.LoserWins}-win perspective = {x.LoserPoints - x.WinnerPoints}"
                }).ToList();

                var upsetCount = results.Count(r => r.IsUpset);
                var negativeCount = results.Count(r => r.Delta < 0);

                return Ok(new
                {
                    year = targetYear,
                    totalGames = results.Count,
                    upsetCount,
                    negativeDeltas = negativeCount,
                    shouldHaveNegatives = upsetCount > 0,
                    problem = upsetCount > 0 && negativeCount == 0 ? "Logic error: upsets exist but no negative deltas!" : null,
                    sampleGames = results.Take(20)
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error running diagnostic");
                return StatusCode(500, "An error occurred during diagnostic.");
            }
        }
    }
}