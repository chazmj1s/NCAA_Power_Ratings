using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NCAA_Power_Ratings.Models;
using NCAA_Power_Ratings.Interfaces;
using NCAA_Power_Ratings.Services;

namespace NCAA_Power_Ratings.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameDataController(
        IGameDataService gameDataService, 
        TeamMetricsService teamMetrics,
        ILogger<GameDataController> logger) : ControllerBase
    {

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
    }
}