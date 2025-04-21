using MediatR;

namespace Odyssey.HRMS.MonoApp.Commands.UpdateJourney;

internal sealed class UpdateJourneyHandler: IRequestHandler<UpdateJourneyCommand, UpdateJourneyResult>
{
    public async Task<UpdateJourneyResult> Handle(UpdateJourneyCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}