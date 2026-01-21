using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NCAA_Rankings.Models;
using NCAA_Rankings.Interfaces;

namespace NCAA_Rankings.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameDataController : ControllerBase
    {
        private readonly IGameDataService gameDataService;
        private readonly ILogger<GameDataController> logger;

        public GameDataController(IGameDataService gameDataService, ILogger<GameDataController> logger)
        {
            this.gameDataService = gameDataService;
            this.logger = logger;
        }

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
        [HttpPost("updateRecords")]
        public async Task<IActionResult> UpdateRecords([FromQuery] int? year)
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
    }
}