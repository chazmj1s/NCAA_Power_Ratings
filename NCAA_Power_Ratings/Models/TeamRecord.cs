using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NCAA_Power_Ratings.Models
{
    [Table("TeamRecords")]
    public class TeamRecord
    {

        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("TeamID")]
        public int TeamID { get; set; }

        [Column("Year", TypeName = "smallint")]
        public short Year { get; set; }

        [Column("Wins", TypeName = "tinyint")]
        public byte Wins { get; set; }

        [Column("Losses", TypeName = "tinyint")]
        public byte Losses { get; set; }

        [Column("PointsFor")]
        public int PointsFor { get; set; }

        [Column("PointsAgainst")]
        public int PointsAgainst { get; set; }

        [Column("BaseSOS", TypeName = "decimal(10,3)")]
        public decimal? BaseSOS { get; set; }

        [Column("SubSOS", TypeName = "decimal(10,3)")]
        public decimal? SubSOS { get; set; }

        [Column("CombinedSOS", TypeName = "decimal(10,4)")]
        public decimal? CombinedSOS { get; set; }

        [ForeignKey("TeamID")]
        public Team? Team { get; set; }

        [NotMapped]
        public int RegularSeasonGames => Year switch
        {
            >= 2006 => 12,
            >= 1980 => 11,
            >= 1965 => 10,
            _ => 12 // Default to current standard for years outside range
        };
    }
}