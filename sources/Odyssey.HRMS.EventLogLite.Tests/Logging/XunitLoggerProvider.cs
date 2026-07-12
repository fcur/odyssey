using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Odyssey.HRMS.EventLogLite.Tests.Logging;

public sealed class XunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;
    private readonly LoggerExternalScopeProvider _scopeProvider = new();
    
    public XunitLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new XunitLogger(_output, categoryName, _scopeProvider);
    }

    public void Dispose() { }
}