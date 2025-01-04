using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Calendar.Tests;

public sealed class DateTimeOffsetYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(DateTimeOffset);
    }

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var value = parser.Consume<Scalar>().Value;
        
        if ( !string.IsNullOrEmpty(value) && DateTimeOffset.TryParse(value, out var result))
        {
            return result;
        }

        return DateTimeOffset.MinValue;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var dateTimeOffset = (DateTimeOffset)value!;
        var formatted = dateTimeOffset.ToString();
        var scalar = new Scalar(AnchorName.Empty, TagName.Empty, formatted, ScalarStyle.DoubleQuoted, true, false);
        
        emitter.Emit(scalar);
    }
}