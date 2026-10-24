using Microsoft.EntityFrameworkCore;
using Odyssey.HRMS.Dal.SQLite.Entities;

namespace Odyssey.HRMS.Dal.SQLite;

public class JourneyDbContext: DbContext
{
    internal virtual DbSet<JourneyDal> Journeys { get; set; }

    public JourneyDbContext() { }
    public JourneyDbContext(DbContextOptions<JourneyDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<JourneyDal>(JourneyDal.Setup);
        
        base.OnModelCreating(builder);
    }
}