using NCAA_Rankings.Models;

namespace NCAA_Rankings.Interfaces
{
    public interface IGameDataService
    {
        public  Task<List<Game>> ExtractGameDataHistoryAsync(int? years);
        public  Task<List<Game>> UpdateGameDataForYearAndWeekAsync(List<Game> existingData, int year, int week);
        public Task<int> LoadGameHistoryFromFiles();
        public Task UpdateTeamRecordsAsync(int? targetYear = null, CancellationToken token = default);
    }
}