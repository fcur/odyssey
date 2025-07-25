using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Consumer;
using Odyssey.HRMS.EventLogLite.Producer;

namespace Odyssey.HRMS.EventLogLite;

public class EventLogBuilder
{
    private readonly IServiceProvider _provider;
    private readonly IConfiguration _configuration;

    private readonly List<IEventBroker> _brokers = new();
    private readonly List<IEventProducer> _producers = new();
    private readonly List<IEventConsumer> _consumers = new();

    private EventLogBuilder(IServiceProvider provider, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(configuration);

        _provider = provider;
        _configuration = configuration;
    }

    public IReadOnlyCollection<IEventBroker> GetBrokers() { return _brokers; }
    public IReadOnlyCollection<IEventProducer> GetProducers() { return _producers; }
    public IReadOnlyCollection<IEventConsumer> GetConsumers() { return _consumers; }

    public static EventLogBuilder Create(IServiceProvider provider, IConfiguration configuration)
    {
        return new EventLogBuilder(provider, configuration);
    }

    public EventLogBuilder WithProducers()
    {
        var brokers = _provider.GetServices<IEventBroker>();
        var producers = _provider.GetServices<IEventProducer>();

        _producers.AddRange(producers);
        _brokers.AddRange(brokers);

        return this;
    }

    public EventLogBuilder AddConsumerHandler<TEventConsumer, TEvent>(string groupName)
        where TEventConsumer : IEventConsumerImpl<TEvent>
        where TEvent : class
    {
        var consumerConfiguration = new EventConsumerSettings(groupName);
        var key = $"{EventLogSettings.ConfigurationSectionName}:{EventLogSettings.ConsumerSectionName}:{groupName}";
        _configuration.GetRequiredSection(key).Bind(consumerConfiguration);

        var broker = _provider.GetRequiredService<IEventBroker<TEvent>>();

        for (byte i = 0; i < consumerConfiguration.ReplicaCount; i++)
        {
            var handler = _provider.GetRequiredKeyedService<IEventConsumerImpl<TEvent>>(groupName);
            var logger = _provider.GetRequiredService<ILogger<EventConsumer<TEvent>>>();
            
            var consumer = new EventConsumer<TEvent>(logger, broker, handler, consumerConfiguration, i);
            _consumers.Add(consumer);
            broker.Join(consumer);
        }

        return this;
    }
}