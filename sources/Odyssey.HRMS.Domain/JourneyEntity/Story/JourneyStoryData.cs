using CSharpFunctionalExtensions;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;
using System.Text.Json;

namespace Odyssey.HRMS.Domain.JourneyEntity.Story;

public sealed record JourneyStoryData(Dictionary<JourneyStoryDataKey, JsonElement> Data)
{
    public Result<Dictionary<string, JsonElement>, JourneyStoryError> GetEventBody(IReadOnlyCollection<JourneyActivityTemplateDependency>? activityDependencies)
    {
        var result = new Dictionary<string, JsonElement>();
        if (activityDependencies == null)
        {
            return result;
        }

        foreach (var item in activityDependencies)
        {
            var key = new JourneyStoryDataKey(item.Key, item.Source?.ActivityName, item.Source?.EventName);

            if (!Data.TryGetValue(key, out var jsonValue))
            {
                return JourneyStoryError.MissingDependency(item.Key);
            }

            result[item.Key] = jsonValue;
        }

        return result;
    }

    public bool TryGetValue(JourneyStoryDataKey key, out JsonElement data) => Data.TryGetValue(key, out data);
    
    public void EnrichWithEventResponse(JourneyActivityName activityName, JourneyActivityEventName eventName, StoryEventBody? eventBody)
    {
        if (eventBody == null)
        {
            return;
        }
        
        foreach (var key in eventBody.Data.Keys)
        {
            var dataKey = new JourneyStoryDataKey(key, activityName, eventName);
            Data[dataKey] = eventBody.Data[key];
        }
    }
}


public readonly record struct JourneyStoryDataKey(string Key, JourneyActivityName? ActivityName, JourneyActivityEventName? EventName)
{
    public static JourneyStoryDataKey Create(string key) => new JourneyStoryDataKey(key, null, null);
    public static JourneyStoryDataKey Create(string key, JourneyActivityName activityName, JourneyActivityEventName eventName) => new JourneyStoryDataKey(key, activityName, eventName);

    public override string ToString()
    {
        if (ActivityName != null && EventName != null)
        {
            return $"{ActivityName}:{EventName}:{Key}";
        }
        
        return Key;
    }

    public override int GetHashCode()
    {
        var baseHashCode = base.GetHashCode();
        if (ActivityName != null && EventName != null)
        {
            return  baseHashCode + EqualityComparer<string>.Default.GetHashCode(Key) 
                                 + EqualityComparer<string>.Default.GetHashCode(EventName.Value)
                                 + EqualityComparer<string>.Default.GetHashCode(ActivityName.Value);
        }

        return baseHashCode + EqualityComparer<string>.Default.GetHashCode(Key);
    }
}