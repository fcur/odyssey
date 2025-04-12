namespace Odyssey.HRMS.Domain.JourneyEntity;

public interface IJourneyRepository
{
    Task<Journey?> Find(JourneyId journeyId, CancellationToken cancellationToken = default);
    
    Task Insert(Journey journey, CancellationToken cancellationToken = default);
    
    Task Update(Journey journey, CancellationToken cancellationToken = default);
}