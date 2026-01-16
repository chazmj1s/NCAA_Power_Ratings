using System.ComponentModel.DataAnnotations.Schema;

namespace NCAA_Rankings.Models
{
    [Table("AvgScoreDeltas")]
    public class AvgScoreDelta
    {
        // maps to tinyint (0..255) in SQL; SQL has constraints to limit to 0..20
        public byte Team1Wins { get; set; }

        public byte Team2Wins { get; set; }

        [Column(TypeName = "decimal(5,4)")]
        public decimal AverageScoreDelta { get; set; }

        public int? SampleSize { get; set; }
    }
}