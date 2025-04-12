using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Odyssey.HRMS.Dal.SQLite;

public sealed class DatabaseMigrationService : IHostedService
{
    private readonly IServiceScopeFactory  _scopeFactory;
    private readonly IMigrator  _migrator;

    public DatabaseMigrationService(IServiceScopeFactory scopeFactory, IMigrator migrator)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(migrator);
        
        _scopeFactory = scopeFactory;
        _migrator = migrator;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string? targetMigration = null;
        using var scope = _scopeFactory.CreateScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<JourneyDbContext>();
        
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await _migrator.MigrateAsync(targetMigration, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}