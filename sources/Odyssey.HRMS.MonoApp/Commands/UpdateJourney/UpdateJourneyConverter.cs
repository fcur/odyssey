using Odyssey.HRMS.MonoApp.Entities.Journey;

namespace Odyssey.HRMS.MonoApp.Commands.UpdateJourney;

internal static class UpdateJourneyConverter
{
    internal static UpdateJourneyCommand ToUpdateJourneyCommand(this UpdateJourneyRequestDto request)
    {


        return new UpdateJourneyCommand();
    }
}