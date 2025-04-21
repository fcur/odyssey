using Microsoft.EntityFrameworkCore;
using Odyssey.HRMS.Domain.JourneyEntity;

namespace Odyssey.HRMS.Dal.SQLite;

public sealed class JourneyRepository : IJourneyRepository
{
    private readonly JourneyDbContext _dbContext;

    public JourneyRepository(JourneyDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public async Task<Journey?> Find(JourneyId journeyId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Journeys.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == journeyId.Value, cancellationToken);

        return entity?.ToDomain();
    }

    public async Task<JourneyId> Insert(Journey journey, CancellationToken cancellationToken = default)
    {
        var entity = journey.ToDal();
        var result = await _dbContext.Journeys.AddAsync(entity, cancellationToken);

        // MAYBE: publish journey-changed event

        return new JourneyId(result.Entity.Id);
    }

    public async Task Update(Journey journey, CancellationToken cancellationToken = default)
    {
        var entity = journey.ToDal();
        _ = _dbContext.Journeys.Update(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // MAYBE: publish journey-changed event
    }
}