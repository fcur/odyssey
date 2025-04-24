using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Odyssey.HRMS.EventLogLite;

public interface IEventProducer
{
}

public interface IEventConsumer
{
}

public sealed class EventLogBuilder(IServiceCollection services, IConfigurationSection section)
{
    private readonly List<IEventProducer> _producers = new();
    private readonly List<IEventConsumer> _consumers = new();

    public static EventLogBuilder Create(IServiceCollection services, IConfigurationSection configuration)
    {
        return new EventLogBuilder(services, configuration);
    }

    public EventLogBuilder AddProducer<TEvent>(string configurationSection) where TEvent : class
    {
        var producerConfiguration = new EventProducerSettings();

        section.GetRequiredSection(configurationSection).Bind(producerConfiguration);
        var producer = new EventProducer<TEvent>(producerConfiguration);

        return this;
    }

    public EventLogBuilder AddConsumer<TEvent>(string configurationSection) where TEvent : class
    {
        var consumerConfiguration = new EventConsumerSettings();

        section.GetRequiredSection(configurationSection).Bind(consumerConfiguration);
        var consumer = new EventConsumer<TEvent>(consumerConfiguration);

        return this;
    }
}

public sealed class EventProducer<TEvent>(EventProducerSettings settings) : IEventProducer where TEvent : class
{
    private readonly EventProducerSettings _settings = settings;
}

public sealed class EventProducerSettings
{
}

public sealed class EventConsumer<TEvent>(EventConsumerSettings settings) : IEventConsumer
    where TEvent : class
{
    private readonly EventConsumerSettings _settings = settings;


    public Task Start()
    {
        return Task.CompletedTask;
    }

    public Task Stop()
    {
        return Task.CompletedTask;
    }
}

public sealed class EventConsumerSettings
{
}