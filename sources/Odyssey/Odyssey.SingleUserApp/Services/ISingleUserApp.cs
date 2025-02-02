namespace Odyssey.SingleUserApp.Services;

public interface ISingleUserApp
{
    Task Start(CancellationToken token = default);
}