using Microsoft.EntityFrameworkCore;
using Odyssey.HRMS.Domain.JourneyEntity;
using Odyssey.HRMS.EventLogLite;

namespace Odyssey.HRMS.Dal.SQLite;

public sealed class JourneyRepository : IJourneyRepository
{
    private readonly JourneyDbContext _dbContext;
    private readonly IEventProducer<JourneyChangedEvent> _domainEventProducer;

    public JourneyRepository(JourneyDbContext dbContext, IEventProducer<JourneyChangedEvent> domainEventProducer)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(domainEventProducer);

        _dbContext = dbContext;
        _domainEventProducer = domainEventProducer;
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

        await PublishDomainEvents(journey, cancellationToken);

        return new JourneyId(result.Entity.Id);
    }

    public async Task Update(Journey journey, CancellationToken cancellationToken = default)
    {
        var entity = journey.ToDal();
        _ = _dbContext.Journeys.Update(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await PublishDomainEvents(journey, cancellationToken);
    }

    private async Task PublishDomainEvents(Journey journey, CancellationToken cancellationToken = default)
    {
        while (journey.TryDequeueEvent(out var domainEvent))
        {
            await _domainEventProducer.Publish(domainEvent, cancellationToken);
        }
    }
}