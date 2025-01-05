using Odyssey.Calendar;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Odyssey.Calendar.Tests.YamlConverter;

public sealed class UserIdYamlConverter: IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(UserId);
    }

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var value = parser.Consume<Scalar>().Value;

        if (Guid.TryParse(value, out var guid))
        {
            return new UserId(guid);
        }

        throw new NotSupportedException(nameof(UserId));
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var data = (UserId)value!;
        var formatted = data.Value.ToString("D");
        var scalar = new Scalar(AnchorName.Empty, TagName.Empty, formatted, ScalarStyle.DoubleQuoted, true, true);
        emitter.Emit(scalar);
    }
}