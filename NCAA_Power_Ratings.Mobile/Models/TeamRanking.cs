namespace NCAA_Power_Ratings.Mobile.Models
{
    /// <summary>
    /// Represents a team with power rating and ranking information
    /// </summary>
    public class TeamRanking
    {
        public int TeamID { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string? Conference { get; set; }
        public string? ConferenceAbbr { get; set; }
        public string? Division { get; set; }
        public int Rank { get; set; }
        public decimal? Ranking { get; set; }
        public int Year { get; set; }
        public byte Wins { get; set; }
        public byte Losses { get; set; }
        public decimal? BaseSOS { get; set; }
        public decimal? CombinedSOS { get; set; }

        public string Record => $"{Wins}-{Losses}";
        public string DisplayRank => $"#{Rank}";
        public string DisplayRanking => Ranking?.ToString("F4") ?? "N/A";
        public string DisplaySOS => CombinedSOS?.ToString("F4") ?? "N/A";
        public bool IsTop25 => Rank <= 25;
    }
}
