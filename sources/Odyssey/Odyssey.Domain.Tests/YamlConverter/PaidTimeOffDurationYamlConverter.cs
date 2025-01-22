// using Odyssey.Domain;
// using YamlDotNet.Core;
// using YamlDotNet.Core.Events;
// using YamlDotNet.Serialization;
//
// namespace Odyssey.Domain.Tests.YamlConverter;
//
// public sealed class PaidTimeOffDurationYamlConverter: IYamlTypeConverter
// {
//     public bool Accepts(Type type)
//     {
//         return type == typeof(PaidTimeOffDuration);
//     }
//
//     public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
//     {
//         var value = parser.Consume<Scalar>().Value;
//         
//         if (!string.IsNullOrEmpty(value) && TimeSpan.TryParse(value, out var result))
//         {
//             return new PaidTimeOffDuration(result);
//         }
//         throw new NotSupportedException(nameof(PaidTimeOffDuration));
//     }
//
//     public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
//     {
//         var data = (PaidTimeOffDuration)value!;
//         var formatted = data.Value.ToString();
//         var scalar = new Scalar(AnchorName.Empty, TagName.Empty, formatted, ScalarStyle.DoubleQuoted, true, true);
//         emitter.Emit(scalar);
//     }
// }