using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Odyssey.HRMS.Domain.JourneyEntity;

namespace Odyssey.HRMS.Dal.SQLite;

public static class Extensions
{
    public static IServiceCollection ConfigureDb(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContextPool<JourneyDbContext>(builder =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            connectionString ??= "Data Source=:memory:";
            builder.UseSqlite(connectionString);

            builder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        services.AddHostedService<DatabaseMigrationService>();

        services.AddTransient<IJourneyRepository, JourneyRepository>();

        return services;
    }
}