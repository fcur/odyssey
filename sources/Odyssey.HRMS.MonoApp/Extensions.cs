using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Consumer;
using Odyssey.HRMS.EventLogLite.Producer;

namespace Odyssey.HRMS.MonoApp;

public static class Extensions
{
    public static IServiceCollection RegisterProducer<TEvent>(this IServiceCollection services, IConfigurationRoot configuration) where TEvent : class
    {
        var producerConfiguration = new EventProducerSettings();
        var key = $"{EventLogSettings.ConfigurationSectionName}:{EventLogSettings.ProducerSectionName}:{typeof(TEvent).Name}";
        configuration.GetRequiredSection(key).Bind(producerConfiguration);

        var broker = new FileEventLogBroker<TEvent>(new EventLogTopic(producerConfiguration.TopicName, producerConfiguration.Partitions));
        var producer = new EventProducer<TEvent>(broker, producerConfiguration);

        services.AddSingleton<IEventBroker<TEvent>>(broker);
        services.AddSingleton<IEventBroker>(broker);
        services.AddSingleton<IEventProducer<TEvent>>(producer);
        services.AddSingleton<IEventProducer>(producer);

        return services;
    }

    public static IServiceCollection RegisterConsumer<TEventConsumerImpl, TEvent>(this IServiceCollection services, string groupName, int replicaCount = 1)
        where TEventConsumerImpl : IEventConsumerImpl<TEvent>
        where TEvent : class
    {
        services.AddKeyedTransient(typeof(IEventConsumerImpl<TEvent>), groupName, typeof(TEventConsumerImpl));
        
        return services;
    }
}