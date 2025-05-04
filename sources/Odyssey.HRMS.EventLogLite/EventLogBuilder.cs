using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Consumer;
using Odyssey.HRMS.EventLogLite.Producer;

namespace Odyssey.HRMS.EventLogLite;

public class EventLogBuilder
{
    private readonly IServiceProvider _provider;
    private readonly IConfiguration _configuration;

    private readonly List<IEventProducer> _producers = new();
    private readonly List<IEventConsumer> _consumers = new();

    private EventLogBuilder(IServiceProvider provider, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(configuration);

        _provider = provider;
        _configuration = configuration;
    }

    public IReadOnlyCollection<IEventProducer> GetProducers() { return _producers; }
    public IReadOnlyCollection<IEventConsumer> GetConsumers() {  return _consumers;}
    
    public static EventLogBuilder Create(IServiceProvider provider, IConfiguration configuration)
    {
        return new EventLogBuilder(provider, configuration);
    }

    public EventLogBuilder WithProducers()
    {
        var producers = _provider.GetServices<IEventProducer>();
        
        _producers.AddRange(producers);
        
        return this;
    }
    
    public EventLogBuilder AddProducer<TEvent>() where TEvent : class
    {
        var producerConfiguration = new EventProducerSettings();
        var key = $"{EventLogSettings.ConfigurationSectionName}:{EventLogSettings.ProducerSectionName}:{typeof(TEvent).Name}";
        _configuration.GetRequiredSection(key).Bind(producerConfiguration);
    
        var producer = new EventProducer<TEvent>(producerConfiguration);
        
        _producers.Add(producer);
        return this;
    }

    public EventLogBuilder AddConsumerHandler<TEventConsumer, TEvent>(string groupName)  
        where TEventConsumer : IEventConsumerImpl<TEvent>
        where TEvent : class
    {
        var consumerConfiguration = new EventConsumerSettings(groupName);
        var key = $"{EventLogSettings.ConfigurationSectionName}:{EventLogSettings.ConsumerSectionName}:{groupName}";
        _configuration.GetRequiredSection(key).Bind(consumerConfiguration);

        var handler = _provider.GetRequiredKeyedService<IEventConsumerImpl<TEvent>>(groupName);
        var consumer = new EventConsumer<TEvent>(consumerConfiguration, handler.Handle);
        
        _consumers.Add(consumer);
        return this;

    }
}