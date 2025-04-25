using Odyssey.HRMS.EventLogLite;

namespace Odyssey.HRMS.MonoApp;

public sealed class EventLogSetupService : IHostedService
{
    private readonly IReadOnlyCollection<IEventProducer> _producers;
    private readonly IReadOnlyCollection<IEventConsumer> _consumers;

    public EventLogSetupService(IServiceProvider provider, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(configuration);
        
        _producers = provider.GetServices<IEventProducer>().ToArray();
        _consumers = provider.GetServices<IEventConsumer>().ToArray();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var tasks = _producers.Select(v => v.Start(cancellationToken)).ToArray();
        await Task.WhenAll(tasks);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var tasks = _producers.Select(v=>v.Stop(cancellationToken)).ToArray();
        await Task.WhenAll(tasks);
    }
}