// Rivalry seed data based on historical analysis
// Source: 60+ years of NCAA data, categorized by intensity and longevity
// Tier meanings:
//   EPIC: Century+ history, anything can happen, highest variance
//   NATIONAL: Major cross-regional rivalries, significant variance
//   STATE: In-state rivalries, moderate to high variance

using NCAA_Power_Ratings.Models;

namespace NCAA_Power_Ratings.Data
{
    public static class RivalrySeedData
    {
        public static List<RivalryMetadata> GetRivalries()
        {
            return new List<RivalryMetadata>
            {
                // EPIC TIER - Expected variance ratio: 1.75x
                new() { Team1Name = "Ohio State", Team2Name = "Michigan", RivalryName = "The Game", Tier = "EPIC", SeriesAge = 131 },
                new() { Team1Name = "Alabama", Team2Name = "Auburn", RivalryName = "Iron Bowl", Tier = "EPIC", SeriesAge = 129 },
                new() { Team1Name = "Texas", Team2Name = "Oklahoma", RivalryName = "Red River Rivalry", Tier = "EPIC", SeriesAge = 118 },

                // NATIONAL TIER - Expected variance ratio: 1.5x
                new() { Team1Name = "Army", Team2Name = "Navy", RivalryName = "Army-Navy Game", Tier = "NATIONAL", SeriesAge = 135 },
                new() { Team1Name = "LSU", Team2Name = "Alabama", RivalryName = "LSU-Alabama", Tier = "NATIONAL", SeriesAge = 107 },
                new() { Team1Name = "Tennessee", Team2Name = "Alabama", RivalryName = "Third Saturday in October", Tier = "NATIONAL", SeriesAge = 107 },
                new() { Team1Name = "Florida", Team2Name = "Georgia", RivalryName = "World's Largest Outdoor Cocktail Party", Tier = "NATIONAL", SeriesAge = 102 },
                new() { Team1Name = "Notre Dame", Team2Name = "USC", RivalryName = "Notre Dame-USC", Tier = "NATIONAL", SeriesAge = 97 },
                new() { Team1Name = "Penn State", Team2Name = "Ohio State", RivalryName = "Penn State-Ohio State", Tier = "NATIONAL", SeriesAge = 96 },
                new() { Team1Name = "Florida", Team2Name = "Florida State", RivalryName = "Florida-Florida State", Tier = "NATIONAL", SeriesAge = 66 },

                // STATE TIER - Expected variance ratio: 1.3x
                new() { Team1Name = "Washington", Team2Name = "Washington State", RivalryName = "Apple Cup", Tier = "STATE", SeriesAge = 133 },
                new() { Team1Name = "Georgia", Team2Name = "Georgia Tech", RivalryName = "Clean, Old-Fashioned Hate", Tier = "STATE", SeriesAge = 131 },
                new() { Team1Name = "Texas", Team2Name = "Texas A&M", RivalryName = "Lone Star Showdown", Tier = "STATE", SeriesAge = 130 },
                new() { Team1Name = "Mississippi", Team2Name = "Mississippi State", RivalryName = "Egg Bowl", Tier = "STATE", SeriesAge = 130 },
                new() { Team1Name = "Oregon", Team2Name = "Oregon State", RivalryName = "Civil War", Tier = "STATE", SeriesAge = 130 },
                new() { Team1Name = "Auburn", Team2Name = "Georgia", RivalryName = "Deep South's Oldest Rivalry", Tier = "STATE", SeriesAge = 129 },
                new() { Team1Name = "Stanford", Team2Name = "California", RivalryName = "Big Game", Tier = "STATE", SeriesAge = 126 },
                new() { Team1Name = "Michigan", Team2Name = "Michigan State", RivalryName = "Paul Bunyan Trophy", Tier = "STATE", SeriesAge = 123 },
                new() { Team1Name = "Clemson", Team2Name = "South Carolina", RivalryName = "Palmetto Bowl", Tier = "STATE", SeriesAge = 121 },
                new() { Team1Name = "Oklahoma", Team2Name = "Oklahoma State", RivalryName = "Bedlam", Tier = "STATE", SeriesAge = 118 },
                new() { Team1Name = "UCLA", Team2Name = "USC", RivalryName = "Victory Bell", Tier = "STATE", SeriesAge = 95 },
                new() { Team1Name = "Nebraska", Team2Name = "Iowa", RivalryName = "Heroes Game", Tier = "STATE", SeriesAge = 51 }
            };
        }

        public class RivalryMetadata
        {
            public string Team1Name { get; set; } = string.Empty;
            public string Team2Name { get; set; } = string.Empty;
            public string RivalryName { get; set; } = string.Empty;
            public string Tier { get; set; } = string.Empty;
            public int SeriesAge { get; set; }

            public double GetExpectedVarianceMultiplier()
            {
                return Tier switch
                {
                    "EPIC" => 1.75,
                    "NATIONAL" => 1.5,
                    "STATE" => 1.3,
                    _ => 1.0
                };
            }
        }
    }
}
