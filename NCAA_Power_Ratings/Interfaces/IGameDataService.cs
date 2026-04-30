using NCAA_Power_Ratings.Models;

namespace NCAA_Power_Ratings.Interfaces
{
    public interface IGameDataService
    {
        public  Task<List<Game>> ExtractGameDataHistoryAsync(int? years);
        public  Task<int> UpdateGameDataForYearAndWeekAsync(int year, int week, CancellationToken token = default);
        public Task<int> LoadGameHistoryFromFiles();
        public Task<int> LoadTeamDataFromFile();
        public Task UpdateTeamRecordsAsync(int? targetYear = null, CancellationToken token = default);
    }
}