using MediatR;

namespace Odyssey.HRMS.MonoApp.Commands.GetJourneys;

internal sealed class GetJourneysHandler:  IRequestHandler<GetJourneysCommand, GetJourneysResult>
{
    public async Task<GetJourneysResult> Handle(GetJourneysCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}