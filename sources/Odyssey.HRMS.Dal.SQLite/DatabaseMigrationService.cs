using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Odyssey.HRMS.Dal.SQLite;

public sealed class DatabaseMigrationService : IHostedService
{
    private readonly IServiceScopeFactory  _scopeFactory;

    public DatabaseMigrationService(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        
        _scopeFactory = scopeFactory;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string? targetMigration = null;
        
        using var scope = _scopeFactory.CreateScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<JourneyDbContext>();
        
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await dbContext.Database.MigrateAsync(targetMigration, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}