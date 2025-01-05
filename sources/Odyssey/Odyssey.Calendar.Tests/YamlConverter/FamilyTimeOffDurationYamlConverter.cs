using Odyssey.Calendar;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Odyssey.Calendar.Tests.YamlConverter;

public sealed class FamilyTimeOffDurationYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(FamilyTimeOffDuration);
    }

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var value = parser.Consume<Scalar>().Value;

        if (!string.IsNullOrEmpty(value) && TimeSpan.TryParse(value, out var result))
        {
            return new FamilyTimeOffDuration(result);
        }

        throw new NotSupportedException(nameof(FamilyTimeOffDuration));
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var data = (FamilyTimeOffDuration)value!;
        var formatted = data.Value.ToString();
        var scalar = new Scalar(AnchorName.Empty, TagName.Empty, formatted, ScalarStyle.DoubleQuoted, true, true);
        emitter.Emit(scalar);
    }
}