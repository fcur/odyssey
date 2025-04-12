using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Odyssey.HRMS.SingleUserApp.Extensions;
using Odyssey.HRMS.SingleUserApp.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.UseSerilog();
builder.AddServices();

using var host = builder.Build();

var app = host.Services.GetRequiredService<ISingleUserApp>();

await host.StartAsync();
await app.Start();
await host.StopAsync();