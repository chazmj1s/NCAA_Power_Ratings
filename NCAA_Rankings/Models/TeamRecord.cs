using Microsoft.EntityFrameworkCore;

namespace NCAA_Rankings.Models
{
    public class NCAAContext : DbContext
    {
        public DbSet<Game> Games { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<AvgScoreDelta> AvgScoreDeltas { get; set; }
        public DbSet<TeamRecord> TeamRecords { get; set; }
    }
}