using System.Diagnostics.CodeAnalysis;

namespace Odyssey.HRMS.Domain.JourneyEntity.Activity;

public record struct JourneyActivityStatus(string Value) : IParsable<JourneyActivityStatus>
{
    private const string DraftKey = "DRAFT";
    private const string ReadyKey = "READY";
    private const string StartedKey = "STARTED";
    private const string FinishedKey = "FINISHED";
    private const string DeletedKey = "DELETED";

    public static readonly JourneyActivityStatus Draft = new(DraftKey);
    public static readonly JourneyActivityStatus Ready = new(ReadyKey);
    public static readonly JourneyActivityStatus Started = new(StartedKey);
    public static readonly JourneyActivityStatus Finished = new(FinishedKey);
    public static readonly JourneyActivityStatus Deleted = new(DeletedKey);

    public static JourneyActivityStatus Parse(string value, IFormatProvider? provider)
    {
        var target = value.ToUpperInvariant();

        return target switch
        {
            DraftKey => Draft,
            ReadyKey => Ready,
            StartedKey => Started,
            DeletedKey => Deleted,
            FinishedKey => Finished,
            _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(JourneyActivityStatus)} '{value}'")
        };
    }

    public static bool TryParse([NotNullWhen(true)] string? value, IFormatProvider? provider, out JourneyActivityStatus result)
    {
        result = default;

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
            result = default;
            return false;
        }
    }
}