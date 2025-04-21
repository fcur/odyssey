using System.Diagnostics.CodeAnalysis;

namespace Odyssey.HRMS.Domain.JourneyEntity;

public record struct JourneyStatus(string Value): IParsable<JourneyStatus>
{
    private const string DraftKey = "DRAFT";
    private const string ReadyKey = "READY";
    private const string StartedKey = "STARTED";
    private const string FinishedKey = "FINISHED";
    private const string DeletedKey = "DELETED";
    
    public static readonly JourneyStatus Draft = new (DraftKey);
    public static readonly JourneyStatus Ready = new (ReadyKey);
    public static readonly JourneyStatus Started = new (StartedKey);
    public static readonly JourneyStatus Finished = new (FinishedKey);
    public static readonly JourneyStatus Deleted = new (DeletedKey);
    

    public static JourneyStatus Parse(string value, IFormatProvider? provider)
    {
        var target = value.ToUpperInvariant();

        return target switch
        {
            DraftKey => Draft,
            ReadyKey => Ready,
            StartedKey => Started,
            DeletedKey  => Deleted,
            FinishedKey  => Finished,
            _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(JourneyStatus)} '{value}'")
        };
    }

    public static bool TryParse([NotNullWhen(true)] string? value, IFormatProvider? provider, out JourneyStatus result)
    {
        result = default;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        
        try
        {
            result =  Parse(value, provider);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            result = default;
            return false;
        }
    }
}