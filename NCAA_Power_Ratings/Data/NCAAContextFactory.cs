using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NCAA_Power_Ratings.Data
{
    /// <summary>
    /// Design-time factory for EF Core migrations.
    /// Required because NCAAContext uses primary constructors and can't be
    /// instantiated by EF tools without this.
    /// </summary>
    public class NCAAContextFactory : IDesignTimeDbContextFactory<NCAAContext>
    {
        public NCAAContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<NCAAContext>();
            
            // Use SQLite for migrations
            optionsBuilder.UseSqlite("Data Source=ncaa-rankings.db");
            
            return new NCAAContext(optionsBuilder.Options);
        }
    }
}