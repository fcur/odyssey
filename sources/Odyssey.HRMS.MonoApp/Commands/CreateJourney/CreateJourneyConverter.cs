using Odyssey.HRMS.MonoApp.Entities.Journey;

namespace Odyssey.HRMS.MonoApp.Commands.CreateJourney;

internal static class CreateJourneyConverter
{
    internal static CreateJourneyCommand ToCreateJourneyCommand(this CreateJourneyRequestDto request)
    {


        return new CreateJourneyCommand();
    }
}