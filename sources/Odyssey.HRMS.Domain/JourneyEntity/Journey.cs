using System.Diagnostics.CodeAnalysis;
using CSharpFunctionalExtensions;
using Odyssey.HRMS.Domain.Base;
using Odyssey.HRMS.Domain.JourneyEntity.Activity;
using Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

namespace Odyssey.HRMS.Domain.JourneyEntity;

public sealed record Journey(JourneyId Id, JourneyName Name, IReadOnlyCollection<JourneyActivity> Activities, JourneyStatus Status, JourneyStartup? Startup, JourneyInitializationData? InitializationData, DateTimeOffset ChangedAt, DomainVersion Version, ulong RowVersion)
    : DomainEntity<JourneyId>(Id, ChangedAt, Version)
{
    public static Result<Journey, JourneyValidationError> Create(JourneyName name, IReadOnlyCollection<JourneyActivity> activities, JourneyStartup? startup = null, JourneyInitializationData? initializationData = null)
    {
        var activitiesValidationError = ValidateActivities(activities);

        if (activitiesValidationError.HasValue)
        {
            return activitiesValidationError.Value;
        }

        var id = JourneyId.New();
        var status = JourneyStatus.Draft;
        var changedAt = DateTimeOffset.UtcNow;
        var version = DomainVersion.New;
        var rowVersion = 0UL;

        // warn: 'StartAt' field is required for 'Ready' journeys.
        // > check it on journey status update

        var @event = new JourneyChangedEvent(id, changedAt, version);
        var journey = new Journey(id, name, activities, status, startup, initializationData, changedAt, version, rowVersion);
        journey.EnqueueEvent(@event);

        return journey;
    }

    private static Maybe<JourneyValidationError> ValidateActivities(IReadOnlyCollection<JourneyActivity> activities)
    {
        var sourceActivityEvents = activities.SelectMany(v => v.Events)
            .Where(v => v.Type == JourneyActivityEventType.Source).ToArray();

        if (sourceActivityEvents.Length == 0)
        {
            return JourneyValidationError.MissingSourceActivity;
        }

        var endOfJourneyActivities = activities.Where(v => v.Name == JourneyActivityName.EndOfJourney).ToArray();

        if (endOfJourneyActivities.Length is 0 or > 1)
        {
            return JourneyValidationError.IncorrectEndOfJourneyActivity;
        }

        (JourneyActivityId? Id, int Count) duplicatedJourney = activities
            .GroupBy(v => v.Id)
            .Select(v => (v.Key, Count: v.Count()))
            .FirstOrDefault(v => v.Count > 1);

        if (duplicatedJourney.Id != null)
        {
            return JourneyValidationError.ActivityIdDuplicate(duplicatedJourney.Id);
        }

        return ValidateFlows(activities);
    }

    private static Maybe<JourneyValidationError> ValidateFlows(IReadOnlyCollection<JourneyActivity> activities)
    {
        var handler = new JourneyFlowHandler(activities);
        var maybeError = handler.Handle();

        if (maybeError.HasValue)
        {
            return JourneyValidationError.IncorrectActivitiesFlow(maybeError.Value);
        }
        
        return Maybe<JourneyValidationError>.None;
    }

    private sealed record FlowStep(string Start, string Event, string? Target)
    {
        public static FlowStep Create(JourneyActivityName start, JourneyActivityEventName @event, JourneyActivityName? target)
        {
            return new FlowStep(start.Value, @event.Value, target?.Value);
        }
        public override string ToString() =>  string.IsNullOrEmpty(Target) ? $"{Start}+{Event}→?": $"{Start}+{Event}→{Target}";
    }

    private sealed class JourneyFlowHandler
    {
        private readonly IReadOnlyDictionary<JourneyActivityId, JourneyActivity> _activities;
        private readonly IReadOnlyCollection<JourneyActivity> _sourceActivities;
        private readonly JourneyActivityId _endOfJourneyId;
        // ReSharper disable once CollectionNeverQueried.Local
        private readonly List<List<FlowStep>> _steps;

        internal JourneyFlowHandler(IReadOnlyCollection<JourneyActivity> activities)
        {
            ArgumentNullException.ThrowIfNull(activities);
            
            _sourceActivities = activities
                .Where(act => act.Events.Any(ev => ev.Type == JourneyActivityEventType.Source))
                .ToArray();
            _endOfJourneyId = activities.Single(v => v.Name == JourneyActivityName.EndOfJourney).Id;
            _activities = activities.ToDictionary(v => v.Id, v => v);
            _steps = new List<List<FlowStep>>();
        }

        internal Maybe<string> Handle()
        {
            foreach (var item in _sourceActivities)
            {
                var maybeError = Handle(item.Name, item.Events, new Stack<FlowStep>());
                if (maybeError.HasValue)
                {
                    return maybeError.Value;
                }
            }
            
            return Maybe<string>.None;
        }

        private Maybe<string> Handle(JourneyActivityName name, IReadOnlyCollection<JourneyActivityEvent> events, Stack<FlowStep> flowStepsStack)
        {
            foreach (var item in events)
            {
                if (!TryGetActivity(item.NextActivityId, out var nextActivity))
                {
                    return $"Found invalid activity event '{item}'";
                }

                var flowStep = FlowStep.Create(name, item.Name, nextActivity.Name);

                if (item.NextActivityId == _endOfJourneyId)
                {
                    _steps.Add([..flowStepsStack.ToArray().Reverse(), flowStep]);
                    continue;
                }

                flowStepsStack.Push(flowStep);
                
                var maybeError = Handle(nextActivity.Name, nextActivity.Events, flowStepsStack);
                if (maybeError.HasValue)
                {
                    return maybeError.Value;
                }
                
                flowStepsStack.Pop();
            }
            
            return Maybe<string>.None;
        }

        private bool TryGetActivity(JourneyActivityId? id, [MaybeNullWhen(false)] out JourneyActivity activity)
        {
            if (id == null)
            {
                activity = null;
                return false;
            }

            return _activities.TryGetValue(id, out activity);
        }
    }
}