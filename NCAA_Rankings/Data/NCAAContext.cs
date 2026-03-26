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
                entity.HasKey(e => new { e.Id });

                // preserve SQL types and names
                entity.Property(e => e.Team1Wins)
                      .HasColumnName("Team1Wins")
                      .HasColumnType("tinyint");

                entity.Property(e => e.Team2Wins)
                      .HasColumnName("Team2Wins")
                      .HasColumnType("tinyint");

                entity.Property(e => e.AverageScoreDelta)
                      .HasColumnName("AverageScoreDelta");

                entity.Property(e => e.StDevP)
                      .HasColumnName("StDevP");

                entity.Property(e => e.SampleSize)
                      .HasColumnName("SampleSize");

                // Team table mapping
                modelBuilder.Entity<Team>(entity =>
                {
                    entity.ToTable("Team");

                    // Primary key
                    entity.HasKey(e => e.TeamID);

                    entity.Property(e => e.TeamID)
                          .HasColumnName("TeamID");

                    entity.Property(e => e.Alias)
                          .HasColumnName("Alias");

                    // TeamName column; adjust length or type if your DB differs
                    entity.Property(e => e.TeamName)
                          .HasColumnName("TeamName")
                          .IsRequired();

                    // Add Division and Conference properties
                    entity.Property(e => e.Division)
                          .HasColumnName("Division")
                          .HasColumnType("varchar(20)");

                    entity.Property(e => e.Conference)
                          .HasColumnName("Conference")
                          .HasColumnType("varchar(50)");

                    entity.Property(e => e.ConferenceAbbr)
                          .HasColumnName("ConferenceAbbr")
                          .HasColumnType("varchar(20)");
                });

                // add same check constraints as the DB to keep EF model aligned
            });
            modelBuilder.Entity<AvgScoreDelta>(entity =>
            {
                entity.ToTable("AvgScoreDeltas");

                entity.HasKey(e => new { e.Id });

                entity.Property(e => e.Id)
                      .HasColumnName("Id");

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

                entity.HasKey(e => new { e.Id });

                entity.Property(e => e.Id)
                      .HasColumnName("Id");

                entity.Property(e => e.TeamID)
                      .HasColumnName("TeamID");

                entity.Property(e => e.Year)
                      .HasColumnName("Year")
                      .IsRequired();

                entity.Property(e => e.Wins)
                      .HasColumnName("Wins")
                      .IsRequired();

                entity.Property(e => e.Losses)
                      .HasColumnName("Losses")
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
            modelBuilder.Entity<Game>(entity =>
            {
                entity.ToTable("Game");

                entity.HasKey(e => new { e.Id });

                entity.Property(e => e.Id)
                      .HasColumnName("Id");

                entity.Property(e => e.Year)
                      .HasColumnName("Year");

                entity.Property(e => e.Week)
                      .HasColumnName("Week");

                entity.Property(e => e.WinnerId)
                      .HasColumnName("WinnerId")
                      .IsRequired(); ;

                entity.Property(e => e.WinnerName)
                      .HasColumnName("WinnerName")
                      .IsRequired();

                entity.Property(e => e.WPoints)
                      .HasColumnName("WPoints");

                entity.Property(e => e.LoserId)
                      .HasColumnName("LoserId")
                      .IsRequired(); ;

                entity.Property(e => e.LoserName)
                      .HasColumnName("LoserName")
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
