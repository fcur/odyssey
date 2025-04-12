using Odyssey.HRMS.Domain.JourneyEntity;

namespace Odyssey.HRMS.Dal.SQLite;

public sealed class JourneyRepository: IJourneyRepository
{
    public async Task<Journey?> Find(JourneyId journeyId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task Insert(Journey journey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task Update(Journey journey, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}