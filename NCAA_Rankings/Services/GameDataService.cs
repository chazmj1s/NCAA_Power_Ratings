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
    public class GameDataService(IDbContextFactory<NCAAContext> _contextFactory, RecordProcessor _recordProcessor, NCAAContext _context, IConfiguration _configuration) : IGameDataService
    {
        public async Task<List<Game>> ExtractGameDataHistoryAsync(int? year)
        {
            var gameDataList = new List<Game>();
            using var httpClient = new HttpClient();
            var tasks = new List<Task<List<Game>>>();
            var currentYear = DateTime.Now.Month < 8 ? DateTime.Now.Year - 1 : DateTime.Now.Year;
            var getYear = year ?? currentYear;



            while (getYear <= currentYear)
            {
                string url = $"https://www.sports-reference.com/cfb/years/{getYear}-schedule.html";
                tasks.Add(ExtractGameDataForSingleYearAsync(httpClient, url, getYear));
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
                    await _context.Games.AddRangeAsync(gameDataList);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving games to database: {ex.Message}");
                }
            }

            return gameDataList;
        }

        private async Task<List<Game>> ExtractGameDataForSingleYearAsync(HttpClient httpClient, string url, int year)
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

                var rank = 1;
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

                    int winnerId = _context.Teams.FirstOrDefault(t => t.TeamName == winnerName)?.TeamID ?? -1;
                    int loserId = _context.Teams.FirstOrDefault(t => t.TeamName == loserName)?.TeamID ?? -1;

                    var siteCellText = cells[6].InnerText.Trim();
                    char siteIndicator = siteCellText.Contains('@') ? 'L' : siteCellText.Contains('N') ? 'N' : 'W';

                    var gameData = new Game
                    {
                        Rank = rank++,
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
            string url = $"https://www.sports-reference.com/cfb/years/{year}-schedule.html";
            using var httpClient = new HttpClient();
            var updatedData = new List<Game>(existingData);

            try
            {
                // Fetch new data for the specified year
                var newData = await ExtractGameDataForSingleYearAsync(httpClient, url, year);

                // Remove existing games for the specified year and week
                updatedData.RemoveAll(game => game.Year == year && game.Week == week);

                // Add new games for the specified year and week
                var weekData = newData.FindAll(game => game.Week == week);
                updatedData.AddRange(weekData);

                // Sort the data by year, week, and rank to maintain order
                updatedData.Sort((a, b) =>
                {
                    int yearCompare = a.Year.CompareTo(b.Year);
                    if (yearCompare != 0) return yearCompare;
                    int weekCompare = a.Week.CompareTo(b.Week);
                    if (weekCompare != 0) return weekCompare;
                    return a.Rank.CompareTo(b.Rank);
                });

                Console.WriteLine($"Updated data for year {year}, week {week}. Added {weekData.Count} games.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating data for year {year}, week {week}: {ex.Message}");
            }

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

            // Use Parallel.ForEachAsync for efficient parallel processing of records
            await Parallel.ForEachAsync(
                ReadRecordsAsync(directoryPath, tokenSource.Token), // The async stream of records
                tokenSource.Token, // Cancellation token
                async (recordFields, token) =>
                {
                    await _recordProcessor.ProcessSingleRecordAsync(recordFields, Path.GetFileNameWithoutExtension(directoryPath), _contextFactory, token);
                    recordsProcessed++;
                }
            );

            return recordsProcessed;
        }

        public async IAsyncEnumerable<string[]> ReadRecordsAsync(string directoryPath, [EnumeratorCancellation] CancellationToken token)
        {
            // Get all CSV files in the directory
            foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*.txt"))
            {
                await foreach (var line in File.ReadLinesAsync(filePath, token))
                {
                    // Skip empty lines or headers if necessary
                    if (string.IsNullOrWhiteSpace(line) || line[0].Equals("Rk")) continue;

                    // Split the line by comma, trim whitespace from fields
                    var fields = line.Split(',')
                                     .Select(field => field.Trim())
                                     .ToArray();
                    yield return fields; // Yield the record (fields array)
                }
            }
        }

    }
}
