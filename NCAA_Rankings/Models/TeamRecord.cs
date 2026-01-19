using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NCAA_Rankings.Models
{
    [Table("TeamRecords")]
    public class TeamRecord
    {
        // kept for compatibility with existing code; not mapped to the database
        [NotMapped]
        public int Id { get; set; }

        // Primary key per DbContext configuration / SQL DDL
        [Key]
        [Column("TeamID")]
        public int TeamID { get; set; }

        [Column("Year", TypeName = "smallint")]
        public short Year { get; set; }

        [Column("Wins", TypeName = "tinyint")]
        public byte Wins { get; set; }

        [Column("Losses", TypeName = "tinyint")]
        public byte Losses { get; set; }

        [Column("PointsFor")]
        public int? PointsFor { get; set; }

        [Column("PointsAgainst")]
        public int? PointsAgainst { get; set; }

        [ForeignKey("TeamID")]
        public Team? Team { get; set; }
    }
}