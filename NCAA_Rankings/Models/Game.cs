using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NCAA_Rankings.Models
{
    [Table("Game")]
    public class Game
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("Year")]
        public int Year { get; set; }

        [Column("Week")]
        public int Week { get; set; }

        [Column("WinnerId")]
        public int WinnerId { get; set; }

        [Required]
        [Column("WinnerName", TypeName = "varchar(50)")]
        public required string WinnerName { get; set; }

        [Column("WPoints")]
        public int WPoints { get; set; }

        [Column("LoserId")]
        public int LoserId { get; set; }

        [Required]
        [Column("LoserName", TypeName = "varchar(50)")]
        public required string LoserName { get; set; }

        [Column("LPoints")]
        public int LPoints { get; set; }

        [Column("Location")]
        public char Location { get; set; }

        // computed property - do not map to the database
        [NotMapped]
        public int Spread { get { return WPoints - LPoints; } }
    }
}
