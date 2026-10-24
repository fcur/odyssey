using Microsoft.Extensions.Logging;
using System.Text;
using Xunit.Abstractions;

namespace Odyssey.HRMS.EventLogLite.Tests.Logging;

public sealed class XunitLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;
    private readonly LoggerExternalScopeProvider _scopeProvider;

    public XunitLogger(ITestOutputHelper output, string category, LoggerExternalScopeProvider scopeProvider)
    {
        _output = output;
        _categoryName = category;
        _scopeProvider = scopeProvider;
    }
    public IDisposable BeginScope<TState>(TState state) => _scopeProvider.Push(state);

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var time = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
        var msgBuilder = new StringBuilder($"{time} [{logLevel}] {formatter(state, exception)}, Context: {_categoryName}");
        
        _scopeProvider.ForEachScope((scope, sb) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object>> keyValuePairs)
            {
                foreach (var item in keyValuePairs)
                {
                    sb.Append($", {item.Key}: {item.Value}");
                }
            }
            else
            {
                sb.Append($", Scope: {scope}");
            }

        }, msgBuilder);
        msgBuilder.AppendLine();

        _output.WriteLine(msgBuilder.ToString());
    }
}