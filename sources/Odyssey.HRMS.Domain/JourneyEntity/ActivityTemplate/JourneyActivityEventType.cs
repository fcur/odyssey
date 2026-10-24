using System.Diagnostics.CodeAnalysis;

namespace Odyssey.HRMS.Domain.JourneyEntity.ActivityTemplate;

public abstract record JourneyActivityEventType(string Value): IParsable<JourneyActivityEventType>
{
    protected const string StubKey = "STUB";
    protected const string SourceKey = "SOURCE";
    protected const string FlowKey = "FLOW";
    protected const string SuccessKey = "SUCCESS";
    protected const string FailKey = "FAIL";
    protected const string ExitKey = "EXIT";

    public static readonly JourneyActivityEventType Source = new SourceJourneyActivityEventType();
    public static readonly JourneyActivityEventType Flow = new FlowJourneyActivityEventType();
    public static readonly JourneyActivityEventType Success = new SuccessJourneyActivityEventType();
    public static readonly JourneyActivityEventType Fail = new FailJourneyActivityEventType();
    public static readonly JourneyActivityEventType Exit = new ExitJourneyActivityEventType();
    private static readonly JourneyActivityEventType Stub = new StubJourneyActivityEventType();
    
    public override string ToString() => Value;
    
    public static JourneyActivityEventType Parse(string value, IFormatProvider? provider)
    {
        var target = value.ToUpperInvariant();
        
        return target switch
        {
            SourceKey => Source,
            FlowKey => Flow,
            SuccessKey => Success,
            FailKey => Fail,
            ExitKey => Exit,
            _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(JourneyActivityEventType)} '{value}'")
        };
    }

    public static bool TryParse([NotNullWhen(true)] string? value, IFormatProvider? provider, [MaybeNullWhen(false)] out JourneyActivityEventType result)
    {
        result = Stub;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        try
        {
            result = Parse(value, provider);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            result = Stub;
            return false;
        }
    }
}

public sealed record StubJourneyActivityEventType() : JourneyActivityEventType(StubKey);
public sealed record SourceJourneyActivityEventType() : JourneyActivityEventType(SourceKey);
public sealed record FlowJourneyActivityEventType() : JourneyActivityEventType(FlowKey);
public sealed record SuccessJourneyActivityEventType() : JourneyActivityEventType(SuccessKey);
public sealed record FailJourneyActivityEventType() : JourneyActivityEventType(FailKey);
public sealed record ExitJourneyActivityEventType() : JourneyActivityEventType(ExitKey);