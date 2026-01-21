using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NCAA_Rankings.Models
{
    [Table("Team")]
    public class Team
    {
        [Key]
        [Column("TeamID")]
        public int TeamID { get; set; }

        [Required]
        [Column("TeamName", TypeName = "varchar(50)")]
        public required string TeamName { get; set; }

        [Column("Alias", TypeName = "varchar(50)")]
        public required string? Alias { get; set; }
    }
}