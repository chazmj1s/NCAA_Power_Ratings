using NCAA_Power_Ratings.ModelViews;

namespace NCAA_Power_Ratings.ModelViews
{
    public class TeamScheduleResponse
    {
        public TeamSeasonSummaryView? Summary { get; set; }
        public List<TeamGameResultView> Games { get; set; } = new();
    }
}