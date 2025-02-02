namespace Odyssey.Domain;

public sealed record TimeRounding(TimeSpan Value)
{
    public static TimeRounding Parse(string duration)
    {
        return new TimeRounding(TimeSpan.Parse(duration));
    }
}