using Microsoft.EntityFrameworkCore;
using NCAA_Rankings.Models;

namespace NCAA_Rankings.Data
{
    public class NCAAContext(DbContextOptions<NCAAContext> options) : DbContext(options)
    {
        public DbSet<Game> Games { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<AvgScoreDelta> AvgScoreDeltas { get; set; }
        public DbSet<TeamRecord> TeamRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AvgScoreDelta>(entity =>
            {
                entity.ToTable("AvgScoreDeltas");

                // composite primary key
                entity.HasKey(e => new { e.Team1Wins, e.Team2Wins });

                // preserve SQL types and names
                entity.Property(e => e.Team1Wins)
                      .HasColumnName("Team1Wins")
                      .HasColumnType("tinyint");

                entity.Property(e => e.Team2Wins)
                      .HasColumnName("Team2Wins")
                      .HasColumnType("tinyint");

                entity.Property(e => e.AverageScoreDelta)
                      .HasColumnName("AverageScoreDelta")
                      .HasColumnType("decimal(5,4)");

                entity.Property(e => e.SampleSize)
                      .HasColumnName("SampleSize");

                // add same check constraints as the DB to keep EF model aligned
            });
            modelBuilder.Entity<AvgScoreDelta>(entity =>
            {
                entity.ToTable("AvgScoreDeltas");

                entity.HasKey(e => new { e.Team1Wins, e.Team2Wins });

                entity.Property(e => e.Team1Wins)
                      .HasColumnName("Team1Wins")
                      .HasColumnType("tinyint");

                entity.Property(e => e.Team2Wins)
                      .HasColumnName("Team2Wins")
                      .HasColumnType("tinyint");

                entity.Property(e => e.AverageScoreDelta)
                      .HasColumnName("AverageScoreDelta")
                      .HasColumnType("decimal(5,4)");

                entity.Property(e => e.SampleSize)
                      .HasColumnName("SampleSize");

            });
            modelBuilder.Entity<TeamRecord>(entity =>
            {
                entity.ToTable("TeamRecords");

                // Primary key is TeamID per your SQL DDL
                entity.HasKey(e => e.TeamID);

                entity.Property(e => e.TeamID)
                      .HasColumnName("TeamID");

                entity.Property(e => e.Year)
                      .HasColumnName("Year")
                      .HasColumnType("smallint")
                      .IsRequired();

                entity.Property(e => e.Wins)
                      .HasColumnName("Wins")
                      .HasColumnType("tinyint")
                      .IsRequired();

                entity.Property(e => e.Losses)
                      .HasColumnName("Losses")
                      .HasColumnType("tinyint")
                      .IsRequired();

                entity.Property(e => e.PointsFor)
                      .HasColumnName("PointsFor");

                entity.Property(e => e.PointsAgainst)
                      .HasColumnName("PointsAgainst");

                // FK to Team.TeamID
                entity.HasOne(e => e.Team)
                      .WithMany()
                      .HasForeignKey(e => e.TeamID)
                      .HasConstraintName("FK_TeamRecords_Team");

            });

            // Game table mapping
            modelBuilder.Entity<Game>(entity =>
            {
                entity.ToTable("Game");

                // composite key: Year, Week, WinnerId, LoserId
                entity.HasKey(e => new { e.Year, e.Week, e.WinnerId, e.LoserId });

                entity.Property(e => e.Year)
                      .HasColumnName("Year");

                entity.Property(e => e.Week)
                      .HasColumnName("Week");

                entity.Property(e => e.Rank)
                      .HasColumnName("Rank")
                      .ValueGeneratedNever();

                entity.Property(e => e.WinnerId)
                      .HasColumnName("WinnerId")
                      .IsRequired(); ;

                entity.Property(e => e.WinnerName)
                      .HasColumnName("WinnerName")
                      .HasColumnType("varchar(50)")
                      .IsRequired();

                entity.Property(e => e.WPoints)
                      .HasColumnName("WPoints");

                entity.Property(e => e.LoserId)
                      .HasColumnName("LoserId")
                      .IsRequired(); ;

                entity.Property(e => e.LoserName)
                      .HasColumnName("LoserName")
                      .HasColumnType("varchar(50)")
                      .IsRequired();

                entity.Property(e => e.LPoints)
                      .HasColumnName("LPoints");

                entity.Property(e => e.Location)
                      .HasColumnName("Location");

                // Spread is a computed CLR-only property; ensure EF doesn't attempt to map it.
                entity.Ignore(e => e.Spread);
            });


            // other model configuration (Games, Teams) can go here as needed
        }
    }
}
