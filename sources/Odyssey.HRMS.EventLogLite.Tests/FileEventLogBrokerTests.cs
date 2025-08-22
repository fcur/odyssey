using AutoFixture.Xunit2;
using Odyssey.HRMS.EventLogLite.Entities;
using System.Diagnostics.CodeAnalysis;

namespace Odyssey.HRMS.EventLogLite.Tests;

[ExcludeFromCodeCoverage]
public sealed class FileEventLogBrokerTests : IAsyncLifetime, IClassFixture<FileLogBrokerFixture>
{
    private readonly FileLogBrokerFixture _fixture;

    // ReSharper disable once ConvertToPrimaryConstructor
    public FileEventLogBrokerTests(FileLogBrokerFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory, AutoData]
    public async Task TestLogEvent(string key, TestEvent payload)
    {
        var cts = new CancellationTokenSource();
        var request = new LogRequest<TestEvent> { Key = key, Payload = payload };

        var broker = _fixture.GetBroker();

        var logResult = await broker.LogEvent(request, cts.Token);
    }


    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}