using MediatR;
using Odyssey.HRMS.Domain.JourneyEntity;

namespace Odyssey.HRMS.MonoApp.Commands.CreateJourney;

internal sealed class CreateJourneyHandler: IRequestHandler<CreateJourneyCommand, CreateJourneyResult>
{
    private readonly IJourneyRepository _journeyRepository;
    
    public CreateJourneyHandler(IJourneyRepository journeyRepository)
    {
        ArgumentNullException.ThrowIfNull(journeyRepository);
        
        _journeyRepository = journeyRepository;
    }
    
    public async Task<CreateJourneyResult> Handle(CreateJourneyCommand request, CancellationToken cancellationToken)
    {
        
        throw new NotImplementedException();
    }
}