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

builder.Services.RegisterProducer<TestEvent>(builder.Configuration)
    .RegisterProducer<JourneyChangedEvent>(builder.Configuration)
    .RegisterProducer<JourneyStoryChangedEvent>(builder.Configuration)
    .RegisterConsumer<TestEvent>(builder.Configuration, "test1")
    .RegisterConsumer<TestEvent>(builder.Configuration, "test2");
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