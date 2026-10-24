using Odyssey.HRMS.MonoApp.Entities.Journey;
using Refit;

namespace Odyssey.HRMS.MonoApp.Clients;

public interface IJourneyApi
{
    [Get("/api/journeys/{id}")]
    Task<JourneyDto> GetEmployee([Query]Guid id);
    
    [Put("/api/journeys/")]
    Task<JourneyDto> CreateEmployee([Body] CreateJourneyRequestDto body);
    
    [Put("/api/journeys/{id}")]
    Task<JourneyDto> UpdateEmployee([Query]Guid id, [Body] UpdateJourneyRequestDto body);
}