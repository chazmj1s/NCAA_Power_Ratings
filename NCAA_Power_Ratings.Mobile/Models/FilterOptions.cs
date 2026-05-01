namespace NCAA_Power_Ratings.Mobile.Models
{
    /// <summary>
    /// Filter options for team rankings
    /// </summary>
    public enum RankingFilter
    {
        All,
        Top25,
        Conference,
        Division
    }

    /// <summary>
    /// Sort options for team rankings
    /// </summary>
    public enum RankingSort
    {
        Rank,
        TeamName,
        PowerRating,
        Record,
        Conference,
        SOS
    }
}
