using Odyssey.HRMS.Dal.SQLite;
using Odyssey.HRMS.Domain.JourneyEntity;
using Odyssey.HRMS.Domain.JourneyEntity.Story;
using Odyssey.HRMS.EventLogLite;
using Odyssey.HRMS.MonoApp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.ConfigureDb(builder.Configuration.GetConnectionString("DefaultConnection"));

builder.Services
    .RegisterProducer<JourneyChangedEvent>(builder.Configuration)
    .RegisterProducer<JourneyStoryChangedEvent>(builder.Configuration)
    .RegisterProducer<TestEvent>(builder.Configuration)
    .RegisterConsumer<TestEventConsumer1, TestEvent>("Test1")
    .RegisterConsumer<TestEventConsumer2, TestEvent>("Test2");

builder.Services.AddHostedService<EventLogSetupService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.Run();