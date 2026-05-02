namespace NCAA_Power_Ratings.Mobile.Models
{
    public class GameResult
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public int Week { get; set; }

        public string WinnerName { get; set; } = string.Empty;
        public int WinnerId { get; set; }
        public string WinnerConf { get; set; } = string.Empty;
        public string WinnerTier { get; set; } = string.Empty;
        public int WPoints { get; set; }

        public string LoserName { get; set; } = string.Empty;
        public int LoserId { get; set; }
        public string LoserConf { get; set; } = string.Empty;
        public string LoserTier { get; set; } = string.Empty;
        public int LPoints { get; set; }

        public char Location { get; set; }

        public int ActualOU { get; set; }
        public double? ProjWinnerScore { get; set; }
        public double? ProjLoserScore { get; set; }
        public double? ProjOU { get; set; }

        // Display helpers
        public string Score => $"{WPoints}-{LPoints}";
        public string ProjScore => ProjWinnerScore.HasValue && ProjLoserScore.HasValue
            ? $"{ProjWinnerScore:F1}-{ProjLoserScore:F1}"
            : "N/A";
        public string DisplayOU => $"{ActualOU}";
        public string DisplayProjOU => ProjOU.HasValue ? $"{ProjOU:F1}" : "N/A";

        /// <summary>Winner name with (H) appended when the winner was the home team.</summary>
        public string WinnerDisplay => Location == 'W' ? $"{WinnerName} (H)" : WinnerName;

        /// <summary>Loser name with (H) appended when the loser was the home team.</summary>
        public string LoserDisplay  => Location == 'L' ? $"{LoserName} (H)"  : LoserName;

        /// <summary>Used to drive the same filter as the Rankings page.</summary>
        public string WinnerFilterKey => WinnerTier;
        public string LoserFilterKey  => LoserTier;
    }
}
