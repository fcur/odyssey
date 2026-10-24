using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Odyssey.HRMS.EventLogLite.Base;

public interface IResultProcessor
{
    Task ForceStop();
}

public interface IResultProcessor<TResult> : IResultProcessor where TResult : class
{
}

public sealed class ResultProcessor<TResult> : IAsyncDisposable, IResultProcessor<TResult> where TResult : class
{
    private readonly Channel<TResult> _channel;
    private readonly Task _processingTask;
    private readonly CancellationTokenSource _cts;
    private readonly ILogger _logger;

    public ResultProcessor(ILogger<ResultProcessor<TResult>> logger)
    {
        _logger = logger;
        _channel = Channel.CreateUnbounded<TResult>();
        _cts = new CancellationTokenSource();
        _processingTask = ProcessInternal(_cts.Token);
    }

    private async Task ProcessInternal(CancellationToken ct)
    {
        try
        {
            var ops = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
            await Parallel.ForEachAsync(_channel.Reader.ReadAllAsync(ct), ops, async (item, token) =>
            {
                await Task.Delay(100, token);
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning($"Processing for {nameof(TResult)} cancelled");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        
        try
        {
            await _processingTask;
        }
        catch (Exception ex) when (ex is OperationCanceledException)
        {

        }
        finally
        {
            _cts.Dispose();
        }
        // ReSharper disable once GCSuppressFinalizeForTypeWithoutDestructor
        GC.SuppressFinalize(this);
    }

    public Task ForceStop()
    {
        _channel.Writer.TryComplete();
        return _cts.CancelAsync();
    }
}