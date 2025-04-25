using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Odyssey.HRMS.EventLogLite;

public static class Extensions
{
    public static IServiceCollection RegisterProducer<TEvent>(this IServiceCollection services,  IConfigurationRoot configuration) where TEvent : class
    {
        var producerConfiguration = new EventProducerSettings();
        var key = $"{EventLogSettings.ConfigurationSectionName}:{EventLogSettings.ProducerSectionName}:{typeof(TEvent).Name}";
        configuration.GetRequiredSection(key).Bind(producerConfiguration);

        var producer = new EventProducer<TEvent>(producerConfiguration);

        services.AddSingleton<IEventProducer<TEvent>>(producer);
        services.AddSingleton<IEventProducer>(producer);
        
        return services;
    }
    
    public static IServiceCollection RegisterConsumer<TEvent>(this IServiceCollection services,  IConfigurationRoot configuration) where TEvent : class
    {
        throw new NotImplementedException();
        
        
        var consumerConfiguration = new EventConsumerSettings();
        var key = $"{EventLogSettings.ConfigurationSectionName}:{EventLogSettings.ConsumerSectionName}:{typeof(TEvent).Name}";
        configuration.GetRequiredSection(key).Bind(consumerConfiguration);

        var consumer = new EventConsumer<TEvent>(consumerConfiguration);

        services.AddSingleton<IEventConsumer<TEvent>>(consumer);
        services.AddSingleton<IEventConsumer>(consumer);
        
        return services;
    }
}