using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NCAA_Rankings.Models
{
    public class Game
    {
        public int Rank { get; set; }
        public int Year { get; set; }
        public int Week { get; set; }
        public required string Winner { get; set; }
        public int WPoints { get; set; }
        public required string Loser { get; set; }
        public int LPoints { get; set; }
        public int Spread {  get { return WPoints - LPoints; } }

    }
}
