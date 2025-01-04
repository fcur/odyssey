using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Calendar.Tests;

public sealed class StartDateYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(StartDate);
    }

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var value = parser.Consume<Scalar>().Value;

        if (!string.IsNullOrEmpty(value) && DateTimeOffset.TryParse(value, out var result))
        {
            return new StartDate(result);
        }

        return new StartDate(DateTimeOffset.UtcNow);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var startDate = (StartDate)value!;

        var formatted = startDate.Date.ToString();
        var scalar = new Scalar(AnchorName.Empty, TagName.Empty, formatted, ScalarStyle.DoubleQuoted, true, true);

        emitter.Emit(scalar);
    }
}