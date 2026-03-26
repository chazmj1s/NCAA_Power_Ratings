using NCAA_Rankings.ModelViews;

namespace NCAA_Rankings.ModelViews
{
    public class TeamScheduleResponse
    {
        public TeamSeasonSummaryView? Summary { get; set; }
        public List<TeamGameResultView> Games { get; set; } = new();
    }
}