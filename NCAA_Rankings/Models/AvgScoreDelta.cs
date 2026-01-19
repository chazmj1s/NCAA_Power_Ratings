using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NCAA_Rankings.Models
{
    [Table("AvgScoreDeltas")]
    public class AvgScoreDelta
    {
        // composite key configured in DbContext fluent API
        [Column("Team1Wins", TypeName = "tinyint")]
        public byte Team1Wins { get; set; }

        [Column("Team2Wins", TypeName = "tinyint")]
        public byte Team2Wins { get; set; }

        [Column("AverageScoreDelta", TypeName = "decimal(5,4)")]
        public decimal AverageScoreDelta { get; set; }

        [Column("SampleSize")]
        public int? SampleSize { get; set; }
    }
}