namespace NCAA_Power_Ratings.Mobile.Models
{
    public class GameResult
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public int Week { get; set; }

        /// <summary>Sequential position assigned by the ViewModel after load — used for "original order" sort.</summary>
        public int SequenceNumber { get; set; }

        /// <summary>True for odd sequence numbers — drives alternating row background.</summary>
        public bool IsOddRow => SequenceNumber % 2 != 0;

        public string WinnerName { get; set; } = string.Empty;
        public string WinnerShortName { get; set; } = string.Empty;
        public int WinnerId { get; set; }
        public string WinnerConf { get; set; } = string.Empty;
        public string WinnerTier { get; set; } = string.Empty;
        public int WPoints { get; set; }

        public string LoserName { get; set; } = string.Empty;
        public string LoserShortName { get; set; } = string.Empty;
        public int LoserId { get; set; }
        public string LoserConf { get; set; } = string.Empty;
        public string LoserTier { get; set; } = string.Empty;
        public int LPoints { get; set; }

        public char Location { get; set; }

        public int ActualOU { get; set; }
        public double? ProjWinnerScore { get; set; }
        public double? ProjLoserScore { get; set; }
        public double? ProjOU { get; set; }

        // --- Actual display ---
        public string Score       => $"{WPoints}-{LPoints}";
        public int    ActualMargin => WPoints - LPoints;
        public string DisplayOU   => $"{ActualOU}";

        // --- Projected display (all rounded to nearest integer) ---
        public bool HasProjection => ProjWinnerScore.HasValue && ProjLoserScore.HasValue;
        public string ProjScore   => HasProjection
            ? $"{(int)Math.Round(ProjWinnerScore!.Value)}-{(int)Math.Round(ProjLoserScore!.Value)}"
            : "–";
        public string DisplayProjMargin => HasProjection
            ? $"{(int)Math.Round(ProjWinnerScore!.Value - ProjLoserScore!.Value)}"
            : "–";
        public string DisplayProjOU => ProjOU.HasValue ? $"{(int)Math.Round(ProjOU.Value)}" : "–";

        /// <summary>Winner short name (with (H) if home), falling back to full name.</summary>
        public string WinnerDisplay => Location == 'W'
            ? $"{(!string.IsNullOrEmpty(WinnerShortName) ? WinnerShortName : WinnerName)} (H)"
            : (!string.IsNullOrEmpty(WinnerShortName) ? WinnerShortName : WinnerName);

        /// <summary>Loser short name (with (H) if home), falling back to full name.</summary>
        public string LoserDisplay => Location == 'L'
            ? $"{(!string.IsNullOrEmpty(LoserShortName) ? LoserShortName : LoserName)} (H)"
            : (!string.IsNullOrEmpty(LoserShortName) ? LoserShortName : LoserName);
    }
}
