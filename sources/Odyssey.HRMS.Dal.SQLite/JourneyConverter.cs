using Odyssey.HRMS.Dal.SQLite.Entities;
using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.JourneyEntity;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;
using System.Text.Json;

namespace Odyssey.HRMS.Dal.SQLite;

internal static class JourneyConverter
{
    internal static Journey ToDomain(this JourneyDal entity, IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(entity, nameof(JourneyDal));

        var journeyId = new JourneyId(entity.Id);
        var journeyName = new JourneyName(entity.Name);
        var activities = entity.Activities.Select(v=> ToDomain(v, provider)).ToArray();
        var status = JourneyStatus.Parse(entity.Status, provider);
        var startup = entity.Startup?.ToDomain();
        var initializationData = entity.InitializationData?.ToDomain();
        var changedAt = entity.ChangedAt;
        var domainVersion = new DomainVersion(entity.Version);
        var rowVersion = entity.RowVersion;
        
        return new Journey(journeyId, journeyName, activities, status, startup, initializationData, changedAt, domainVersion, rowVersion);
    }

    internal static JourneyDal ToDal(this Journey entity, IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(entity, nameof(Journey));

        var id = entity.Id.Value;
        var name = entity.Name.Value;
        var activities = entity.Activities.Select(ToDal).ToArray();
        var status = entity.Status.Value;
        var startup = entity.Startup?.ToDal();
        var initializationData = entity.InitializationData?.ToDal();
        var changedAt = entity.ChangedAt;
        var domainVersion = entity.Version.Value;
        var rowVersion = entity.RowVersion;

        return new JourneyDal() { Id = id, Name  = name, Activities = activities, Status = status, Startup = startup, 
            InitializationData = initializationData,  ChangedAt = changedAt, Version = domainVersion, RowVersion = rowVersion };
    }

    private static JourneyActivity ToDomain(this JourneyActivityDal entity, IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(entity, nameof(JourneyActivityDal));
        
        var id = new JourneyActivityId(entity.Id);
        var name = new JourneyActivityName(entity.Name);
        var status = JourneyActivityStatus.Parse(entity.Status, provider);
        var events = entity.Events.Select(v=>ToDomain(v, provider)).ToArray();
        
        return new JourneyActivity(id,name, status, events);
    }

    private static JourneyActivityDal ToDal(this JourneyActivity entity)
    {
        ArgumentNullException.ThrowIfNull(entity, nameof(JourneyActivity));
        
        var id = entity.Id.Value;
        var name = entity.Name.Value;
        var status = entity.Status.Value;
        var events = entity.Events.Select(ToDal).ToArray();

        return new JourneyActivityDal { Id = id, Name = name, Status = status, Events = events };
    }
    
    private static JourneyStartupDal ToDal(this JourneyStartup entity)
    {
        ArgumentNullException.ThrowIfNull(entity, nameof(JourneyStartup));

        var startAt = entity.StartAt;
        var repeatingRule = entity.RepeatingRule?.Cron;

        return new JourneyStartupDal { StartAt = startAt, RepeatingRule = repeatingRule };
    }

    private static JourneyStartup ToDomain(this JourneyStartupDal entity)
    {
        ArgumentNullException.ThrowIfNull(entity, nameof(JourneyStartupDal));
        
        var startAt = entity.StartAt;
        var repeatingRule = string.IsNullOrEmpty(entity.RepeatingRule)
            ? JourneyRepeatingRule.Unset
            : new JourneyRepeatingRule(entity.RepeatingRule);

        return new JourneyStartup(startAt, repeatingRule);
    }
    
    private static Dictionary<string, JsonElement> ToDal(this JourneyInitializationData entity)
    {
        ArgumentNullException.ThrowIfNull(entity, nameof(JourneyInitializationData));

        return entity.Data.ToDictionary();
    }

    private static JourneyInitializationData ToDomain(this Dictionary<string, JsonElement> entity)
    {
        ArgumentNullException.ThrowIfNull(entity, nameof(JourneyDal.InitializationData));

        return new JourneyInitializationData(entity);
    }
    
    private static JourneyActivityEventDal ToDal(this JourneyActivityEvent entity)
    {
        ArgumentNullException.ThrowIfNull(entity, nameof(JourneyActivityEvent));

        var name = entity.Name.Value;
        var type = entity.Type.Value;
        var nextActivityId = entity.NextActivityId?.Value;

        return new JourneyActivityEventDal { Name = name, Type = type, NextActivityId = nextActivityId };
    }

    private static JourneyActivityEvent ToDomain(this JourneyActivityEventDal entity, IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(entity, nameof(JourneyActivityEventDal));

        var name = new JourneyActivityEventName(entity.Name);
        var type = JourneyActivityEventType.Parse(entity.Type, provider);
        var nextActivityId = entity.NextActivityId.HasValue
            ? new JourneyActivityId(entity.NextActivityId.Value)
            : JourneyActivityId.Unset;
        
        return new JourneyActivityEvent(name, type, nextActivityId);
    }
}