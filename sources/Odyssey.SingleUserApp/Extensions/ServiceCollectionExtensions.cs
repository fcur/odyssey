using Microsoft.Extensions.DependencyInjection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Odyssey.SingleUserApp.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddYamlDeserializer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        var deserializerBuilder = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance);

        return services.AddSingleton<IDeserializer>(deserializerBuilder.Build());
    }
}