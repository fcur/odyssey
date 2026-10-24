using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Consumer;
using Odyssey.HRMS.EventLogLite.Producer;

namespace Odyssey.HRMS.MonoApp;

public static class Extensions
{
    private const string OffsetLoggerKey = "offsetLogger";
    
    public static IServiceCollection RegisterProducer<TEvent>(this IServiceCollection services, IConfigurationRoot configuration) where TEvent : class
    {
        var serviceProvider = services.BuildServiceProvider();
        var brokerConfiguration = new EventBrokerSettings();
        var brokerConfigKey = $"{EventLogSettings.ConfigurationSectionName}:{EventLogSettings.BrokerSectionName}:{EventLogSettings.OffsetsTopicSection}";
        configuration.GetRequiredSection(brokerConfigKey).Bind(brokerConfiguration);

        var producerConfiguration = new EventProducerSettings();
        var producerConfigKey = $"{EventLogSettings.ConfigurationSectionName}:{EventLogSettings.ProducerSectionName}:{typeof(TEvent).Name}";
        configuration.GetRequiredSection(producerConfigKey).Bind(producerConfiguration);

        var offsetLogger = serviceProvider.GetKeyedService<IFileEventLogger>(OffsetLoggerKey);
        if (offsetLogger == null)
        {
            offsetLogger = new JsonFileEventLogger();
            services.AddKeyedSingleton<IFileEventLogger>(OffsetLoggerKey);
        }
        
        var eventTopic = new EventLogTopic(producerConfiguration.TopicName, producerConfiguration.Partitions);
        
        // TODO: register broker separately 
        var eventLogger = new JsonFileEventLogger();
        
        var brokerLogger = serviceProvider.GetRequiredService<ILogger<FileEventLogBroker>>();
        var broker = new FileEventLogBroker(brokerLogger, brokerConfiguration, eventLogger, offsetLogger, eventTopic);
        
        var producerLogger = serviceProvider.GetRequiredService<ILogger<EventProducer<TEvent>>>();
        var producer = new EventProducer<TEvent>(producerLogger, broker, producerConfiguration);

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