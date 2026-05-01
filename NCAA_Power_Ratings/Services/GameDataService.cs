using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using NCAA_Power_Ratings.Data;
using NCAA_Power_Ratings.Extensions;
using NCAA_Power_Ratings.Interfaces;
using NCAA_Power_Ratings.Models;
using NCAA_Power_Ratings.ModelViews;
using NCAA_Power_Ratings.Utilities;
using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NCAA_Power_Ratings.Services
{
    public class GameDataService(
        IDbContextFactory<NCAAContext> _contextFactory, 
        RecordProcessor _recordProcessor, 
        ScoreDeltaCalculator _scoreDeltaCalc, 
        IConfiguration _configuration,
        IHttpClientFactory _httpClientFactory) : IGameDataService
    {
        public async Task<List<Game>> ExtractGameDataHistoryAsync(int? year)
        {
            var gameDataList = new List<Game>();
            var httpClient = _httpClientFactory.CreateClient();
            var tasks = new List<Task<List<Game>>>();
            var currentYear = DateTime.Now.Month < 8 ? DateTime.Now.Year - 1 : DateTime.Now.Year;
            var getYear = year ?? currentYear;

            await using var context = _contextFactory.CreateDbContext();

            while (getYear <= currentYear)
            {
                string url = $"https://www.sports-reference.com/cfb/years/{getYear}-schedule.html";
                tasks.Add(ExtractGameDataForSingleYearAsync(httpClient, url, context, getYear));
                getYear++;
            }

            var results = await Task.WhenAll(tasks);
            foreach (var result in results)
            {
                gameDataList.AddRange(result);
            }

            if (gameDataList.Count > 0)
            {
                try
                {
                    await context.Games.AddRangeAsync(gameDataList);
                    await context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving games to database: {ex.Message}");
                }
            }

            return gameDataList;
        }

        private static async Task<List<Game>> ExtractGameDataForSingleYearAsync(HttpClient httpClient, string url, NCAAContext context, int year)
        {
            var gameDataList = new List<Game>();

            try
            {
                var html = await httpClient.GetStringAsync(url);
                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(html);

                var table = htmlDocument.DocumentNode.SelectSingleNode("//table[@id='schedule']");
                if (table == null)
                {
                    Console.WriteLine($"Schedule table not found for year {year}.");
                    return gameDataList;
                }

                var rows = table.SelectNodes(".//tbody/tr");
                if (rows == null)
                {
                    Console.WriteLine($"No rows found in the schedule table for year {year}.");
                    return gameDataList;
                }

                var regex = @"\(\d+\)&nbsp;";
                foreach (var row in rows)
                {
                    if (row.GetAttributeValue("class", "").Contains("thead"))
                        continue;

                    var cells = row.SelectNodes(".//td");
                    if (cells == null || cells.Count < 10)
                        continue;

                    // Extract winnerName and loserName from the appropriate cells
                    var winnerName = Regex.Replace(cells[4].InnerText, regex, "").Trim();
                    var loserName = Regex.Replace(cells[7].InnerText, regex, "").Trim();

                    int winnerId = context.Teams.FirstOrDefault(t => t.TeamName == winnerName)?.TeamID ?? -1;
                    int loserId = context.Teams.FirstOrDefault(t => t.TeamName == loserName)?.TeamID ?? -1;

                    var siteCellText = cells[6].InnerText.Trim();
                    char siteIndicator = siteCellText.Contains('@') ? 'L' : siteCellText.Contains('N') ? 'N' : 'W';

                    var gameData = new Game
                    {
                        Week = int.TryParse(cells[0].InnerText.Trim(), out int week) ? week : 0,
                        WinnerId = winnerId,
                        WinnerName = winnerName,
                        WPoints = int.TryParse(cells[5].InnerText.Trim(), out int wpoints) ? wpoints : 0,
                        Location = siteIndicator,
                        LoserId = loserId,
                        LoserName = loserName,
                        LPoints = int.TryParse(cells[8].InnerText.Trim(), out int lpoints) ? lpoints : 0,
                        Year = year
                    };

                    gameDataList.Add(gameData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching data for year {year}: {ex.Message}");
            }

            return gameDataList;
        }

        public async Task<int> UpdateGameDataForYearAndWeekAsync(int year, int week, CancellationToken token = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(token);

            List<Game> scrapedGames;
            bool usedLocalFile = false;

            try
            {
                // Try to fetch fresh data from the web for the specified year
                using var httpClient = new HttpClient();
                string url = $"https://www.sports-reference.com/cfb/years/{year}-schedule.html";
                scrapedGames = await ExtractGameDataForSingleYearAsync(httpClient, url, context, year);
                Console.WriteLine($"Successfully scraped data from web for year {year}");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Web scraping failed: {ex.Message}");

                // Try to fall back to local file
                var dataDirectory = _configuration.GetValue<string>("CustomSettings:FilePath", "NCAA Raw Game Data");
                var filePath = Path.Combine(dataDirectory, $"{year}.txt");

                if (File.Exists(filePath))
                {
                    Console.WriteLine($"Falling back to local file: {filePath}");

                    try
                    {
                        var teams = await context.Teams.ToListAsync(token);
                        scrapedGames = new List<Game>();

                        // Read games from the file
                        await foreach (var recordInfo in ReadRecordsFromFileAsync(filePath, token))
                        {
                            if (recordInfo.Fields.Length >= 9)
                            {
                                var gameData = recordInfo.Fields.ToGame(recordInfo.FileName, teams);
                                scrapedGames.Add(gameData);
                            }
                        }

                        usedLocalFile = true;
                        Console.WriteLine($"Successfully loaded {scrapedGames.Count} games from local file");
                    }
                    catch (Exception fileEx)
                    {
                        Console.WriteLine($"Error reading local file: {fileEx.Message}");
                        throw new InvalidOperationException(
                            $"Web scraping failed and local file read failed. " +
                            $"Please perform a manual scrape to initialize year {year}. " +
                            $"Web error: {ex.Message}, File error: {fileEx.Message}", ex);
                    }
                }
                else
                {
                    Console.WriteLine($"Local file not found: {filePath}");
                    throw new InvalidOperationException(
                        $"Web scraping failed and no local file found at {filePath}. " +
                        $"Please perform a manual scrape to initialize year {year}. " +
                        $"Error: {ex.Message}", ex);
                }
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                Console.WriteLine($"Unexpected error during data fetch: {ex.Message}");
                throw;
            }

            try
            {
                // Filter to only the games for the specified week
                var weekGames = scrapedGames.Where(g => g.Week == week).ToList();

                if (weekGames.Count == 0)
                {
                    Console.WriteLine($"No games found for year {year}, week {week}.");
                    return 0;
                }

                // Load existing games for this year and week from database
                var existing = await context.Games
                    .Where(g => g.Year == year && g.Week == week).ToDictionaryAsync(
                g => (g.WinnerId, g.LoserId),
                g => g,
                token);

                int updated = 0, added = 0;

                foreach (var game in weekGames)
                {
                    var key = (game.WinnerId, game.LoserId);

                    if (existing.TryGetValue(key, out var dbGame))
                    {
                        if (ShouldUpdate(dbGame, game))
                        {
                            UpdateGameProperties(dbGame, game);
                            updated++;
                        }
                    }
                    else
                    {
                        context.Games.Add(game);
                        added++;
                    }
                }

                int changes = await context.SaveChangesAsync(token);

                var source = usedLocalFile ? "local file" : "web scraping";
                Console.WriteLine($"Processed {year}-W{week} from {source}: added {added}, updated {updated}, total changes {changes}");

                return added + updated;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving games to database: {ex.Message}");
                throw;
            }
        }

        private void UpdateGameProperties(Game dbGame, Game game)
        {
            dbGame.WPoints = game.WPoints;
            dbGame.LPoints = game.LPoints;
        }

        private static bool ShouldUpdate(Game dbGame, Game game)
        {
            return dbGame.WinnerId == game.WinnerId &&
                   dbGame.LoserId == game.LoserId &&
                   (game is { WPoints: 0, LPoints: 0 });
        }

        public async Task<int> LoadGameHistoryFromFiles()
        {
            // Retrieve the file path from configuration
            var dataDirectory = _configuration.GetValue<string>("CustomSettings:FilePath", "NCAA Raw Game Data");

            var games = await ProcessDirectoryAsync(dataDirectory);
            await UpdateTeamRecordsAsync();
            await _scoreDeltaCalc.UpdateAvgScoreDeltasTableAsync();

            return games;

        }

        public async Task<int> ProcessDirectoryAsync(string directoryPath)
        {
            Console.WriteLine($"Starting processing in {directoryPath}...");
            var tokenSource = new CancellationTokenSource();
            //var recordProcessor = new RecordProcessor(); // Your processing logic class
            var recordsProcessed = 0;
            var yearIn = Path.GetFileNameWithoutExtension(directoryPath);

            // Use Parallel.ForEachAsync for efficient parallel processing of records
            await Parallel.ForEachAsync(
                ReadRecordsAsync(directoryPath, tokenSource.Token), // The async stream of records
                tokenSource.Token, // Cancellation token
                async (recordInfo, token) =>
                {
                    await _recordProcessor.ProcessSingleRecordAsync(recordInfo.Fields, recordInfo.FileName, token);
                    recordsProcessed++;
                }
            );

            return recordsProcessed;
        }

        /// <summary>
        /// Reads records from a single file.
        /// </summary>
        /// <param name="filePath">Full path to the file to read</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Async enumerable of FileRecord objects</returns>
        public async IAsyncEnumerable<FileRecord> ReadRecordsFromFileAsync(string filePath, [EnumeratorCancellation] CancellationToken token = default)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);

            await foreach (var line in File.ReadLinesAsync(filePath, token))
            {
                // Skip empty lines or headers if necessary
                if (string.IsNullOrEmpty(line) || line.Trim().StartsWith("Rk")) 
                    continue;

                // Split the line by comma, trim whitespace from fields
                var fields = line.Split(',')
                                 .Select(field => field.Trim())
                                 .ToArray();

                yield return new FileRecord(fileName, fields);
            }
        }

        /// <summary>
        /// Reads records from all .txt files in the specified directory.
        /// </summary>
        /// <param name="directoryPath">Path to the directory containing files</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Async enumerable of FileRecord objects from all files</returns>
        public async IAsyncEnumerable<FileRecord> ReadRecordsAsync(string directoryPath, [EnumeratorCancellation] CancellationToken token = default)
        {
            // Get all .txt files in the directory
            foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*.txt"))
            {
                await foreach (var record in ReadRecordsFromFileAsync(filePath, token))
                {
                    yield return record;
                }
            }
        }

        /// <summary>
        /// Processes a single file from the NCAA Raw Game Data directory.
        /// Useful for debugging when you want to test processing a specific file.
        /// </summary>
        /// <param name="filePath">Full path to the file to process</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Number of records processed</returns>
        public async Task<int> ProcessSingleFileAsync(string filePath, CancellationToken token = default)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            Console.WriteLine($"Processing file: {filePath}");
            var recordsProcessed = 0;

            await foreach (var recordInfo in ReadRecordsFromFileAsync(filePath, token))
            {
                await _recordProcessor.ProcessSingleRecordAsync(recordInfo.Fields, recordInfo.FileName, token);
                recordsProcessed++;
            }

            Console.WriteLine($"Completed processing {recordsProcessed} records from {Path.GetFileName(filePath)}");
            return recordsProcessed;
        }

        /// <summary>
        /// Parallel method to UpdateGameDataForYearAndWeekAsync that uses a local file instead of web scraping.
        /// Useful for debugging when the website is inaccessible or for testing with known data.
        /// </summary>
        /// <param name="filePath">Full path to the CSV file containing game data for the year</param>
        /// <param name="year">Year of the games</param>
        /// <param name="week">Week number to filter and update</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Number of games added or updated</returns>
        public async Task<int> UpdateGameDataFromFileAsync(string filePath, int year, int week, CancellationToken token = default)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            await using var context = await _contextFactory.CreateDbContextAsync(token);

            try
            {
                var teams = await context.Teams.ToListAsync(token);
                var fileGames = new List<Game>();

                // Read games from the file
                await foreach (var recordInfo in ReadRecordsFromFileAsync(filePath, token))
                {
                    if (recordInfo.Fields.Length >= 9)
                    {
                        var gameData = recordInfo.Fields.ToGame(recordInfo.FileName, teams);
                        fileGames.Add(gameData);
                    }
                }

                // Filter to only the games for the specified week
                var weekGames = fileGames.Where(g => g.Week == week).ToList();

                if (weekGames.Count == 0)
                {
                    Console.WriteLine($"No games found for year {year}, week {week} in file {Path.GetFileName(filePath)}.");
                    return 0;
                }

                // Load existing games for this year and week from database
                var existing = await context.Games
                    .Where(g => g.Year == year && g.Week == week)
                    .ToDictionaryAsync(
                        g => (g.WinnerId, g.LoserId),
                        g => g,
                        token);

                int updated = 0, added = 0;

                foreach (var game in weekGames)
                {
                    var key = (game.WinnerId, game.LoserId);

                    if (existing.TryGetValue(key, out var dbGame))
                    {
                        if (ShouldUpdate(dbGame, game))
                        {
                            UpdateGameProperties(dbGame, game);
                            updated++;
                        }
                    }
                    else
                    {
                        context.Games.Add(game);
                        added++;
                    }
                }

                int changes = await context.SaveChangesAsync(token);

                Console.WriteLine($"Processed {year}-W{week} from file: added {added}, updated {updated}, total changes {changes}");

                return added + updated;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file for year {year}, week {week}: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateTeamRecordsAsync(int? targetYear = null, CancellationToken token = default)
        {
            // Use an asynchronous factory so the DbContext gets disposed asynchronously.
            await using var context = await _contextFactory.CreateDbContextAsync(token);
            try
            {

                // Project each game to two rows (winner and loser), group and aggregate — equivalent to the SQL WITH+UNION approach.
                var query = context.Games
                    .Where(g => g.Year > 0);

                if (targetYear.HasValue)
                {
                    query = query.Where(g => g.Year == targetYear.Value);
                }

                var winners = query.Select(g => new
                {
                    Year = g.Year,
                    TeamId = g.WinnerId,
                    TeamName = g.WinnerName,
                    Wins = 1,
                    Losses = 0,
                    PointsFor = g.WPoints,
                    PointsAgainst = g.LPoints
                });

                var losers = query.Select(g => new
                {
                    Year = g.Year,
                    TeamId = g.LoserId,
                    TeamName = g.LoserName,
                    Wins = 0,
                    Losses = 1,
                    PointsFor = g.LPoints,
                    PointsAgainst = g.WPoints
                });

                var combined = winners.Concat(losers);

                // Aggregate in the database into a simple DTO (ints), then materialize.
                var grouped = await combined
                    .GroupBy(x => new { x.Year, x.TeamId, x.TeamName })
                    .Select(g => new
                    {
                        Year = g.Key.Year,
                        TeamId = g.Key.TeamId,
                        TeamName = g.Key.TeamName,
                        Wins = g.Sum(x => x.Wins),
                        Losses = g.Sum(x => x.Losses),
                        PointsFor = g.Sum(x => x.PointsFor),
                        PointsAgainst = g.Sum(x => x.PointsAgainst)
                    })
                    .ToListAsync(token);

                if (grouped.Count == 0)
                    return;

                // Map to TeamRecord model with proper casts and upsert.
                var aggregated = grouped.Select(g => new TeamRecord
                {
                    TeamID = g.TeamId,
                    Year = (short)g.Year,
                    Wins = (byte)g.Wins,
                    Losses = (byte)g.Losses,
                    PointsFor = g.PointsFor,
                    PointsAgainst = g.PointsAgainst
                }).ToList();

                // Load existing TeamRecords for affected years into memory for efficient upsert.
                var years = aggregated.Select(a => a.Year).Distinct().ToList();
                var existingRecords = await context.TeamRecords
                    .Where(tr => years.Contains(tr.Year))
                    .ToListAsync(token);

                // Upsert: update existing records, add missing ones.
                foreach (var rec in aggregated)
                {
                    var exist = existingRecords.FirstOrDefault(e => e.TeamID == rec.TeamID && e.Year == rec.Year);
                    if (exist != null)
                    {
                        exist.Wins = rec.Wins;
                        exist.Losses = rec.Losses;
                        exist.PointsFor = rec.PointsFor;
                        exist.PointsAgainst = rec.PointsAgainst;
                        // EF Core will track changes; no explicit Update required.
                    }
                    else
                    {
                        // Avoid adding duplicates if another aggregated entry matches (shouldn't happen after grouping).
                        context.TeamRecords.Add(rec);
                    }
                }

                await context.SaveChangesAsync(token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating team records: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                throw; // Re-throw so the controller can log it properly
            }
        }

        /// <summary>
        /// Gets complete team schedule and summary for a specific team and year.
        /// Returns JSON string with season summary and game-by-game results.
        /// </summary>
        public async Task<string> GetTeamScheduleAsJsonAsync(int teamId, int year, CancellationToken token = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(token);
            
            try
            {
                // First query: Team season summary
                var summary = await context.TeamRecords
                    .Where(tr => tr.TeamID == teamId && tr.Year == year)
                    .Join(
                        context.Teams,
                        tr => tr.TeamID,
                        t => t.TeamID,
                        (tr, t) => new TeamSeasonSummaryView
                        {
                            Year = tr.Year,
                            TeamName = t.TeamName,
                            Wins = tr.Wins,
                            Losses = tr.Losses,
                            PointsFor = tr.PointsFor,
                            PointsAgainst = tr.PointsAgainst
                        })
                    .FirstOrDefaultAsync(token);

                // Second query: Game-by-game results
                var games = await context.Games
                    .Where(g => g.Year == year && (g.WinnerId == teamId || g.LoserId == teamId))
                    .Join(
                        context.Teams,
                        g => g.WinnerId,
                        w => w.TeamID,
                        (g, w) => new { Game = g, Winner = w })
                    .Join(
                        context.Teams,
                        x => x.Game.LoserId,
                        l => l.TeamID,
                        (x, l) => new TeamGameResultView
                        {
                            Week = x.Game.Week,
                            Result = x.Game.WinnerId == teamId ? "Win" : "Loss",
                            Opponent = x.Game.WinnerId == teamId ? x.Game.LoserName : x.Game.WinnerName,
                            Division = x.Game.WinnerId == teamId ? l.Division : x.Winner.Division,
                            Conference = x.Game.WinnerId == teamId ? l.ConferenceAbbr : x.Winner.ConferenceAbbr,
                            Score = x.Game.WinnerId == teamId
                                ? $"{x.Game.WPoints} - {x.Game.LPoints}"
                                : $"{x.Game.LPoints} - {x.Game.WPoints}"
                        })
                    .OrderBy(g => g.Week)
                    .ToListAsync(token);

                var response = new TeamScheduleResponse
                {
                    Summary = summary,
                    Games = games
                };

                // Serialize to JSON with readable formatting
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                return JsonSerializer.Serialize(response, options);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting team schedule for team {teamId}, year {year}: {ex.Message}");
                throw;
            }
        }

        Task<int> IGameDataService.LoadTeamDataFromFile()
        {
            throw new NotImplementedException();
        }
    }
}
