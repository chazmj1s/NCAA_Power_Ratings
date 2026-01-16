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
        [Column("TeamName", TypeName = "varchar(100)")]
        public required string TeamName { get; set; }

        [Column("Division", TypeName = "varchar(3)")]
        public string? Division { get; set; }

        [Column("NickName", TypeName = "varchar(30)")]
        public string? NickName { get; set; }
    }
}