using Microsoft.Maui.Devices;
using NCAA_Power_Ratings.Mobile.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace NCAA_Power_Ratings.Mobile.Services
{
    /// <summary>
    /// Service for calling the NCAA Power Ratings GameData API
    /// </summary>
    public class GameDataApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public GameDataApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;

            // Configure base URL based on platform for local testing
            // TODO: Update this to your deployed API URL
            _baseUrl = DeviceInfo.Platform == DevicePlatform.Android
                ? "http://10.0.2.2:5086/api/productiongamedata"  // Android emulator
                : "http://localhost:5086/api/productiongamedata"; // iOS simulator / desktop
        }

        /// <summary>
        /// Gets power rankings for a specific year
        /// </summary>
        public async Task<List<Models.TeamRanking>?> GetPowerRankingsAsync(int? year = null)
        {
            try
            {
                var currentYear = year ?? DateTime.Now.Year;

                System.Diagnostics.Debug.WriteLine($"[API] ========================================");
                System.Diagnostics.Debug.WriteLine($"[API] Fetching power rankings for year {currentYear}");
                System.Diagnostics.Debug.WriteLine($"[API] Base URL: {_baseUrl}");

                var url = $"{_baseUrl}/powerrankings?year={currentYear}";
                System.Diagnostics.Debug.WriteLine($"[API] Full URL: {url}");

                var response = await _httpClient.GetAsync(url);
                System.Diagnostics.Debug.WriteLine($"[API] Response Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[API] Error Response: {errorContent}");
                    throw new HttpRequestException($"API returned {response.StatusCode}");
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[API] Response length: {jsonContent.Length} characters");
                System.Diagnostics.Debug.WriteLine($"[API] First 500 chars: {(jsonContent.Length > 500 ? jsonContent.Substring(0, 500) : jsonContent)}");

                var rankings = await response.Content.ReadFromJsonAsync<List<Models.TeamRanking>>();

                System.Diagnostics.Debug.WriteLine($"[API] Successfully deserialized {rankings?.Count ?? 0} rankings");

                if (rankings != null && rankings.Count > 0)
                {
                    // Map OverallRank to Rank for backward compatibility
                    foreach (var ranking in rankings)
                    {
                        ranking.Rank = ranking.OverallRank;
                    }

                    System.Diagnostics.Debug.WriteLine($"[API] First team: {rankings[0].TeamName} - Rank: {rankings[0].Rank} ({rankings[0].Tier} #{rankings[0].TierRank}) - Power: {rankings[0].Ranking}");
                    System.Diagnostics.Debug.WriteLine($"[API] Last team: {rankings[^1].TeamName} - Rank: {rankings[^1].Rank} ({rankings[^1].Tier} #{rankings[^1].TierRank}) - Power: {rankings[^1].Ranking}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[API] WARNING: No rankings returned from API!");
                }

                System.Diagnostics.Debug.WriteLine($"[API] ========================================");
                return rankings;
            }
            catch (HttpRequestException httpEx)
            {
                System.Diagnostics.Debug.WriteLine($"[API] ========================================");
                System.Diagnostics.Debug.WriteLine($"[API] HTTP ERROR: {httpEx.Message}");
                System.Diagnostics.Debug.WriteLine($"[API] Is the backend API running at {_baseUrl}?");
                System.Diagnostics.Debug.WriteLine($"[API] Falling back to mock data");
                System.Diagnostics.Debug.WriteLine($"[API] ========================================");

                // Fall back to mock data if API is unavailable
                return await GetMockPowerRankingsAsync(year ?? DateTime.Now.Year);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] ========================================");
                System.Diagnostics.Debug.WriteLine($"[API] ERROR: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[API] Stack: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"[API] Falling back to mock data");
                System.Diagnostics.Debug.WriteLine($"[API] ========================================");

                // Fall back to mock data on error
                return await GetMockPowerRankingsAsync(year ?? DateTime.Now.Year);
            }
        }

        /// <summary>
        /// Gets all FBS teams with id, name, conference, and tier.
        /// </summary>
        public async Task<List<Models.TeamInfo>?> GetTeamsAsync()
        {
            try
            {
                var url = $"{_baseUrl}/teams";
                return await _httpClient.GetFromJsonAsync<List<Models.TeamInfo>>(url);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Error getting teams: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the full season schedule with actual and projected scores.
        /// </summary>
        public async Task<List<Models.GameResult>?> GetScheduleAsync(int? year = null)
        {
            try
            {
                var currentYear = year ?? DateTime.Now.Year;
                var url = $"{_baseUrl}/schedule?year={currentYear}";
                var schedule = await _httpClient.GetFromJsonAsync<List<Models.GameResult>>(url);
                return schedule;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Error getting schedule: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets team schedule as JSON
        /// </summary>
        public async Task<string?> GetTeamScheduleAsync(int teamId, int year)
        {
            try
            {
                // This would call an endpoint like /api/gamedata/teamSchedule
                // TODO: Implement this endpoint in your backend
                var url = $"{_baseUrl}/teamSchedule?teamId={teamId}&year={year}";
                var response = await _httpClient.GetStringAsync(url);
                return response;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting team schedule: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets all named rivalries with series metadata.
        /// Add this method to GameDataApiService.cs
        /// </summary>
        public async Task<List<Models.RivalryInfo>?> GetNamedRivalriesAsync()
        {
            try
            {
                var url = $"{_baseUrl}/rivalries/named";
                return await _httpClient.GetFromJsonAsync<List<Models.RivalryInfo>>(url);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Error getting named rivalries: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Updates game data for a specific week
        /// </summary>
        public async Task<bool> UpdateWeekGamesAsync(int year, int week)
        {
            try
            {
                var url = $"{_baseUrl}/updateWeekGames?year={year}&week={week}";
                var response = await _httpClient.PostAsync(url, null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating week games: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Mock data for testing UI before backend endpoint is ready
        /// TODO: Remove this once backend returns actual data
        /// </summary>
        private async Task<List<Models.TeamRanking>> GetMockPowerRankingsAsync(int year)
        {
            await Task.Delay(500); // Simulate network delay

            var conferences = new[] { "SEC", "Big Ten", "Big 12", "ACC", "Pac-12" };
            var teams = new List<Models.TeamRanking>();

            var random = new Random(42); // Fixed seed for consistent mock data

            for (int i = 1; i <= 133; i++)
            {
                teams.Add(new Models.TeamRanking
                {
                    TeamID = i,
                    TeamName = $"Team {i}",
                    Conference = conferences[i % conferences.Length],
                    ConferenceAbbr = conferences[i % conferences.Length],
                    Division = "FBS",
                    Rank = i,
                    Ranking = (decimal)(100 - (i * 0.5) + random.NextDouble() * 5),
                    Year = year,
                    Wins = (byte)random.Next(0, 13),
                    Losses = (byte)random.Next(0, 6),
                    BaseSOS = (decimal)(random.NextDouble() * 10),
                    CombinedSOS = (decimal)(random.NextDouble() * 15)
                });
            }

            return teams;
        }

        /// <summary>
        /// Gets projected conference standings for all FBS teams.
        /// throughWeek simulates mid-season — games after this week are projected.
        /// </summary>
        public async Task<List<ProjectedTeamStanding>> GetProjectedStandingsAsync(
            int year,
            int? throughWeek = null)
        {
            try
            {
                var url = $"{_baseUrl}/projected-standings?year={year}";
                if (throughWeek.HasValue)
                    url += $"&throughWeek={throughWeek}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var raw = JsonSerializer.Deserialize<List<JsonElement>>(json, options);

                return raw?.Select(r => new ProjectedTeamStanding
                {
                    TeamName = r.GetProperty("teamName").GetString(),
                    Conference = r.GetProperty("conference").GetString(),
                    Division = r.TryGetProperty("division", out var div) && div.ValueKind != JsonValueKind.Null
                                            ? div.GetString() : null,
                    ActualWins = r.GetProperty("actualWins").GetInt32(),
                    ActualLosses = r.GetProperty("actualLosses").GetInt32(),
                    ProjectedWins = r.GetProperty("projectedWins").GetInt32(),
                    ProjectedLosses = r.GetProperty("projectedLosses").GetInt32(),
                    ProjectedWinPct = r.GetProperty("projectedWinPct").GetDouble(),
                    Games = r.GetProperty("games").EnumerateArray().Select(g => new ProjectedGame
                    {
                        Week = g.GetProperty("week").GetInt32(),
                        Opponent = g.GetProperty("opponent").GetString(),
                        Location = g.GetProperty("location").GetString(),
                        Result = g.GetProperty("result").GetString(),
                        Score = g.TryGetProperty("score", out var sc) && sc.ValueKind != JsonValueKind.Null ? sc.GetString() : null,
                        ProjScore = g.TryGetProperty("projScore", out var ps) && ps.ValueKind != JsonValueKind.Null ? ps.GetString() : null,
                        Confidence = g.TryGetProperty("confidence", out var cf) && cf.ValueKind != JsonValueKind.Null ? cf.GetString() : null,
                        Type = g.GetProperty("type").GetString(),
                        NeutralSite = g.GetProperty("neutralSite").GetBoolean()
                    }).ToList()
                }).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Error getting projected standings: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets projected conference championship qualifiers for all FBS conferences.
        /// throughWeek simulates mid-season — games after this week are projected.
        /// </summary>
        public async Task<List<ChampionshipMatchup>> GetProjectedChampionshipQualifiersAsync(
            int year,
            int? throughWeek = null)
        {
            try
            {
                var url = $"{_baseUrl}/projected-championship-qualifiers?year={year}";
                if (throughWeek.HasValue)
                    url += $"&throughWeek={throughWeek}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var raw = JsonSerializer.Deserialize<List<JsonElement>>(json, options);

                return raw?.Select(r => new ChampionshipMatchup
                {
                    Conference = r.GetProperty("conference").GetString(),
                    Format = r.GetProperty("format").GetString(),
                    Qualifier1Method = r.GetProperty("qualifier1Method").GetString(),
                    Qualifier2Method = r.GetProperty("qualifier2Method").GetString(),
                    SimulatedThrough = r.TryGetProperty("simulatedThrough", out var st)
                                            ? st.GetString() : null,
                    TiebreakerLog = r.GetProperty("tiebreakerLog")
                                          .EnumerateArray()
                                          .Select(l => l.GetString())
                                          .ToList(),
                    StubsApplied = r.GetProperty("stubsApplied")
                                          .EnumerateArray()
                                          .Select(l => l.GetString())
                                          .ToList(),
                    Qualifier1 = ParseQualifier(r.GetProperty("qualifier1")),
                    Qualifier2 = ParseQualifier(r.GetProperty("qualifier2")),
                    Contenders = r.GetProperty("contenders").EnumerateArray()
                      .Select(c => new ChampionshipContender
                      {
                          TeamName = c.GetProperty("teamName").GetString(),
                          ConferenceWins = c.GetProperty("conferenceWins").GetInt32(),
                          ConferenceLosses = c.GetProperty("conferenceLosses").GetInt32()
                      }).ToList()
                }).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Error getting championship qualifiers: {ex.Message}");
                return null;
            }
        }

        private static ChampionshipQualifier ParseQualifier(JsonElement q) => new()
        {
            TeamName = q.GetProperty("teamName").GetString(),
            ConferenceWins = q.GetProperty("conferenceWins").GetInt32(),
            ConferenceLosses = q.GetProperty("conferenceLosses").GetInt32(),
            OverallWins = q.GetProperty("overallWins").GetInt32(),
            OverallLosses = q.GetProperty("overallLosses").GetInt32(),
            Division = q.TryGetProperty("division", out var d) && d.ValueKind != JsonValueKind.Null
                                   ? d.GetString() : null
        };
    }
}
