using Microsoft.EntityFrameworkCore;
using NCAA_Power_Ratings.Models;

namespace NCAA_Power_Ratings.Data
{
    public class NCAAContext(DbContextOptions<NCAAContext> options) : DbContext(options)
    {
        public DbSet<Game> Games { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<AvgScoreDelta> AvgScoreDeltas { get; set; }
        public DbSet<TeamRecord> TeamRecords { get; set; }
        public DbSet<MatchupHistory> MatchupHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Game>().Ignore(e => e.Spread);

            // Composite key for MatchupHistory
            modelBuilder.Entity<MatchupHistory>()
                .HasKey(m => new { m.Team1Id, m.Team2Id });
        }
    }
}
