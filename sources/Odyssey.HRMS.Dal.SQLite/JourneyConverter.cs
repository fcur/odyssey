using Odyssey.HRMS.Dal.SQLite.Entities;
using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.JourneyEntity;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;
using System.Text.Json;

namespace Odyssey.HRMS.Dal.SQLite;

internal static class JourneyConverter
{
    internal static Journey ToDomain(this JourneyDal entity, IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(entity, nameof(JourneyDal));

        var journeyId = new JourneyId(entity.Id);
        var journeyName = new JourneyName(entity.Name);
        var activities = entity.Activities.Select(ToDomain).ToArray();
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

        throw new NotImplementedException();
    }


    private static JourneyActivity ToDomain(this JourneyActivityDal entity)
    {
        throw new NotImplementedException();
    }

    private static JourneyActivityDal ToDal(this JourneyActivity entity)
    {
        throw new NotImplementedException();
    }
    
    private static JourneyStartupDal ToDal(this JourneyStartup entity)
    {
        throw new NotImplementedException();
    }

    private static JourneyStartup ToDomain(this JourneyStartupDal entity)
    {
        throw new NotImplementedException();
    }
    
    private static Dictionary<string, JsonElement> ToDal(this JourneyInitializationData entity)
    {
        throw new NotImplementedException();
    }

    private static JourneyInitializationData ToDomain(this Dictionary<string, JsonElement> entity)
    {
        throw new NotImplementedException();
    }
}