using Microsoft.EntityFrameworkCore;
using NCAA_Rankings.Models;

namespace NCAA_Rankings.Data
{
    public class NCAAContext(DbContextOptions<NCAAContext> options) : DbContext(options)
    {
        public DbSet<Game> Games { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<AvgScoreDelta> AvgScoreDeltas { get; set; }

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


            // other model configuration (Games, Teams) can go here as needed
        }
    }
}
