using NCAA_Rankings.Models;

namespace NCAA_Rankings.Interfaces
{
    public interface IGameDataService
    {
        public  Task<List<Game>> ExtractGameDataForYearsAsync(int? years);
        public  Task<List<Game>> UpdateGameDataForYearAndWeekAsync(List<Game> existingData, int year, int week);
    }
}