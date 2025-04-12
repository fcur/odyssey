using System.Text.Json;

namespace Odyssey.HRMS.Domain.JourneyEntity;

public sealed record JourneyInitializationData(IReadOnlyDictionary<string, JsonElement> Data)
{
    public static readonly JourneyInitializationData? Unset = null;

    public static JourneyInitializationData Create(string key, JsonElement rawData)
    {
        var data = new Dictionary<string, JsonElement>() { { key, rawData } };
        return new JourneyInitializationData(data);
    }
}