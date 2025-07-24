using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Consumer;
using Odyssey.HRMS.EventLogLite.Producer;

namespace Odyssey.HRMS.MonoApp;

public static class Extensions
{
    public static IServiceCollection RegisterProducer<TEvent>(this IServiceCollection services, IConfigurationRoot configuration) where TEvent : class
    {
        var brokerConfiguration = new EventBrokerSettings();
        var brokerConfigKey = $"{EventLogSettings.ConfigurationSectionName}:{EventLogSettings.BrokerSectionName}:{EventLogSettings.OffsetsTopicSection}";
        configuration.GetRequiredSection(brokerConfigKey).Bind(brokerConfiguration);

        var producerConfiguration = new EventProducerSettings();
        var producerConfigKey = $"{EventLogSettings.ConfigurationSectionName}:{EventLogSettings.ProducerSectionName}:{typeof(TEvent).Name}";
        configuration.GetRequiredSection(producerConfigKey).Bind(producerConfiguration);

        var topic = new EventLogTopic(producerConfiguration.TopicName, producerConfiguration.Partitions);

        var eventLogger = new JsonFileEventLogger();
        // TODO: register broker separately 
        var broker = new FileEventLogBroker<TEvent>(brokerConfiguration, eventLogger, topic);
        var producer = new EventProducer<TEvent>(broker, producerConfiguration);

        services.AddSingleton<IEventBroker<TEvent>>(broker);
        services.AddSingleton<IEventBroker>(broker);
        services.AddSingleton<IEventProducer<TEvent>>(producer);
        services.AddSingleton<IEventProducer>(producer);

        services.AddTransient<IFileEventLogger, JsonFileEventLogger>();
        
        return services;
    }

    public static IServiceCollection RegisterConsumer<TEventConsumerImpl, TEvent>(this IServiceCollection services, string groupName)
        where TEventConsumerImpl : IEventConsumerImpl<TEvent>
        where TEvent : class
    {
        services.AddKeyedTransient(typeof(IEventConsumerImpl<TEvent>), groupName, typeof(TEventConsumerImpl));
        
        return services;
    }
}