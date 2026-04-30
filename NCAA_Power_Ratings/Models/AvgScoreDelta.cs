using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NCAA_Power_Ratings.Models
{
    [Table("AvgScoreDeltas")]
    public class AvgScoreDelta
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("Team1Wins", TypeName = "tinyint")]
        public byte Team1Wins { get; set; }

        [Column("Team2Wins", TypeName = "tinyint")]
        public byte Team2Wins { get; set; }

        [Column("AverageScoreDelta", TypeName = "decimal(6,2)")]
        public decimal AverageScoreDelta { get; set; }

        [Column("StDevP", TypeName = "decimal(10,8)")]
        public decimal StDevP { get; set; }

        [Column("SampleSize")]
        public int? SampleSize { get; set; }
    }
}