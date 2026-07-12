using Odyssey.HRMS.MonoApp.Entities.Journey;

namespace Odyssey.HRMS.MonoApp.Commands.GetJourneys;

internal static class GetJourneysConverter
{
    internal static GetJourneysCommand ToGetJourneysCommand(this GetJourneysRequestDto request)
    {


        return new GetJourneysCommand();
    }
}