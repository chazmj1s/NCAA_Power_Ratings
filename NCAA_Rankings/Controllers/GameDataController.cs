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
        private readonly IGameDataService _gameDataService;
        private readonly ILogger<GameDataController> _logger;

        public GameDataController(IGameDataService gameDataService, ILogger<GameDataController> logger)
        {
            _gameDataService = gameDataService;
            _logger = logger;
        }

        /// <summary>
        /// Extract game data starting from the provided year up through the current year.
        /// Example: GET /api/gamedata/extract?startYear=2020
        /// </summary>
        [HttpGet("extract")]
        public async Task<ActionResult<List<Game>>> Extract([FromQuery] int? startYear)
        {
            try
            {
                var result = await _gameDataService.ExtractGameDataForYearsAsync(startYear);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting game data for startYear={StartYear}", startYear);
                return StatusCode(500, "An error occurred while extracting game data.");
            }
        }
    }
}