namespace SaturdayPulse.Models
{
    /// <summary>
    /// Persisted game projection snapshot.
    /// One row per (GameId, Year, Week) — history is kept across weekly uploads,
    /// mirroring the WeeklyRankings pattern.
    /// </summary>
    public class Projection
    {
        public int     ProjectionId       { get; set; }
        public int     GameId             { get; set; }
        public int     Year               { get; set; }
        public int     Week               { get; set; }
        public int     HomeTeamId         { get; set; }
        public int     AwayTeamId         { get; set; }

        /// <summary>Positive = home team favored.</summary>
        public decimal PredictedSpread    { get; set; }
        public decimal PredictedTotal     { get; set; }

        /// <summary>0.0 – 1.0</summary>
        public decimal HomeWinProbability { get; set; }
    }
}
