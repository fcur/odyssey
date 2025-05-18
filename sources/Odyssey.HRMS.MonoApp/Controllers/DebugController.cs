using Microsoft.AspNetCore.Mvc;
using Odyssey.HRMS.EventLogLite;
using Odyssey.HRMS.EventLogLite.Base;
using Odyssey.HRMS.EventLogLite.Entities;

namespace Odyssey.HRMS.MonoApp.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class DebugController : ControllerBase
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    private readonly ILogger<DebugController> _logger;
    private readonly IEventProducer<TestEvent> _eventProducer;

    public DebugController(ILogger<DebugController> logger, IEventProducer<TestEvent> eventProducer)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(eventProducer);

        _logger = logger;
        _eventProducer = eventProducer;
    }

    [HttpGet("forecast")]
    public IEnumerable<WeatherForecast> Get(CancellationToken ct)
    {
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
    }

    [HttpPut("test-event")]
    public async Task<IActionResult> PublishTestEvent(CancellationToken ct)
    {
        var timeNow = DateTimeOffset.UtcNow;
        var id = Guid.CreateVersion7(timeNow);
        var sourceContext = nameof(DebugController);
        
        var eventData = new TestEvent { Id = id, OccurredAt = timeNow, SourceContext = sourceContext };
        
        var key = "019673e6-6080-7624-b3ba-0b09df4f7dcd";
        
        _logger.LogInformation("now: {Now}, key: {Key}",timeNow,  key);

        var request = new LogRequest<TestEvent> { Key = key, Payload = eventData };
        
        await _eventProducer.Publish(request, ct);

        return Ok(id);
    }
}