namespace Odyssey.Domain;

public static class EnumerableExtensions
{
    public static IEnumerable<T> ExceptNull<T>(this IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        foreach (var element in source)
        {
            if (element == null)
            {
                continue;
            }

            yield return element;
        }
    }
}