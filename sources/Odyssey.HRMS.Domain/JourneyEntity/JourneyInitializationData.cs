using System.Text.Json;

namespace Odyssey.HRMS.Domain.JourneyEntity;

public sealed record JourneyInitializationData(IReadOnlyDictionary<string, JsonElement> Data)
{
    public static readonly JourneyInitializationData? Unset = null;
}