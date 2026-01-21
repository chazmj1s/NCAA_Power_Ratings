using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using NCAA_Rankings.Data;
using NCAA_Rankings.Interfaces;
using NCAA_Rankings.Models;
using NCAA_Rankings.Utilities;
using System;
using System.Configuration;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NCAA_Rankings.Services
{
    public class GameDataService(IDbContextFactory<NCAAContext> _contextFactory, RecordProcessor _recordProcessor, IConfiguration _configuration) : IGameDataService
    {
        public async Task<List<Game>> ExtractGameDataHistoryAsync(int? year)
        {
            var gameDataList = new List<Game>();
            using var httpClient = new HttpClient();
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

        public async Task<List<Game>> UpdateGameDataForYearAndWeekAsync(List<Game> existingData, int year, int week)
        {
            //string url = $"https://www.sports-reference.com/cfb/years/{year}-schedule.html";
            //using var httpClient = new HttpClient();
            var updatedData = new List<Game>(existingData);
            //await using var context = _contextFactory.CreateDbContext();

            //try
            //{
            //    // Fetch new data for the specified year
            //    var newData = await ExtractGameDataForSingleYearAsync(httpClient, url, context, year);

            //    // Remove existing games for the specified year and week
            //    updatedData.RemoveAll(game => game.Year == year && game.Week == week);

            //    // Add new games for the specified year and week
            //    var weekData = newData.FindAll(game => game.Week == week);
            //    updatedData.AddRange(weekData);

            //    // Sort the data by year, week, and rank to maintain order
            //    updatedData.Sort((a, b) =>
            //    {
            //        int yearCompare = a.Year.CompareTo(b.Year);
            //        if (yearCompare != 0) return yearCompare;
            //        int weekCompare = a.Week.CompareTo(b.Week);
            //        if (weekCompare != 0) return weekCompare;
            //        return a.Rank.CompareTo(b.Rank);
            //    });

            //    Console.WriteLine($"Updated data for year {year}, week {week}. Added {weekData.Count} games.");
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine($"Error updating data for year {year}, week {week}: {ex.Message}");
            //}

            return updatedData;
        }

        public async Task<int> LoadGameHistoryFromFiles()
        {
            // Retrieve the file path from configuration
            var dataDirectory = _configuration.GetValue<string>("CustomSettings:FilePath", "NCAA Raw Game Data");

            return await ProcessDirectoryAsync(dataDirectory);
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

        public async IAsyncEnumerable<FileRecord> ReadRecordsAsync(string directoryPath, [EnumeratorCancellation] CancellationToken token)
        {
            // Get all CSV files in the directory
            foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*.txt"))
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);

                await foreach (var line in File.ReadLinesAsync(filePath, token))
                {
                    // Skip empty lines or headers if necessary
                    if (string.IsNullOrEmpty(line) || line.Trim().StartsWith("Rk")) continue;

                    // Split the line by comma, trim whitespace from fields
                    var fields = line.Split(',')
                                     .Select(field => field.Trim())
                                     .ToArray();

                    yield return new FileRecord(fileName, fields); ; // Yield the record (fields array)
                }
            }
        }

        public async Task UpdateTeamRecordsAsync(int? targetYear = null, CancellationToken token = default)
        {
            // Use an asynchronous factory so the DbContext gets disposed asynchronously.
            await using var context = await _contextFactory.CreateDbContextAsync(token);

            // Project each game to two rows (winner and loser), group and aggregate — equivalent to the SQL WITH+UNION approach.
            var query = context.Games
                .Where(g => g.Year > 0);

            if (targetYear.HasValue)
            {
                query = query.Where(g => g.Year == targetYear.Value);
            }

            var aggregated = await query
                .SelectMany(g => new[]
                {
                new
                {
                    Year = g.Year,
                    TeamId = g.WinnerId,
                    TeamName = g.WinnerName,
                    Wins = 1,
                    Losses = 0,
                    PointsFor = g.WPoints,
                    PointsAgainst = g.LPoints
                },
                new
                {
                    Year = g.Year,
                    TeamId = g.LoserId,
                    TeamName = g.LoserName,
                    Wins = 0,
                    Losses = 1,
                    PointsFor = g.LPoints,
                    PointsAgainst = g.WPoints
                }
                })
                .GroupBy(x => new { x.Year, x.TeamId, x.TeamName })
                .Select(g => new TeamRecord
                {
                    TeamID = g.Key.TeamId,
                    Year = (short)g.Key.Year,
                    Wins = (byte)g.Sum(x => x.Wins),
                    Losses = (byte)g.Sum(x => x.Losses),
                    PointsFor = g.Sum(x => x.PointsFor),
                    PointsAgainst = g.Sum(x => x.PointsAgainst)
                })
                .ToListAsync(token);

            if (aggregated.Count == 0)
                return;

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


    }
}
