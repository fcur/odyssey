using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odyssey.HRMS.Domain.JourneyEntity;

namespace Odyssey.HRMS.Dal.SQLite;

public static class Extensions
{
    public static IServiceCollection ConfigureDb(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<JourneyDbContext>(builder =>
        {
            connectionString ??= "Data Source=:memory:";
            builder.UseSqlite(connectionString);

            builder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });


        services.AddHostedService<DatabaseMigrationService>();
        services.AddTransient<IJourneyRepository, JourneyRepository>();

        return services;
    }
}