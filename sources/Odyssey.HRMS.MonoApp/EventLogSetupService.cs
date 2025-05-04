using Odyssey.HRMS.EventLogLite;
using Odyssey.HRMS.EventLogLite.Base;

namespace Odyssey.HRMS.MonoApp;

public sealed class EventLogSetupService : IHostedService
{
    private readonly IReadOnlyCollection<IEventProducer> _producers;
    private readonly IReadOnlyCollection<IEventConsumer> _consumers;

    public EventLogSetupService(IServiceProvider provider, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(configuration);

        var eventLogBuilder = EventLogBuilder.Create(provider, configuration).WithProducers()
            .AddConsumerHandler<TestEventConsumer1, TestEvent>("Test1")
            .AddConsumerHandler<TestEventConsumer2, TestEvent>("Test2");

        _producers = eventLogBuilder.GetProducers();
        _consumers = eventLogBuilder.GetConsumers();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var producers = _producers.Select(v => v.Start(cancellationToken)).ToArray();
        var consumers = _consumers.Select(v => v.Start(cancellationToken)).ToArray();
        await Task.WhenAll(producers.Concat(consumers));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var producers = _producers.Select(v => v.Stop(cancellationToken)).ToArray();
        var consumers = _consumers.Select(v => v.Stop(cancellationToken)).ToArray();
        await Task.WhenAll(producers.Concat(consumers));
    }
}