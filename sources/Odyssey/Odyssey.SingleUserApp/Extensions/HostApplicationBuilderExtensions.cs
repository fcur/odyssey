using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Odyssey.SingleUserApp.Services;
using Serilog;

namespace Odyssey.SingleUserApp.Extensions;

internal static class HostApplicationBuilderExtensions
{
    internal static HostApplicationBuilder UseSerilog(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();

            var loggerConfig = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration);
            var logger = loggerConfig.CreateLogger();
            loggingBuilder.AddSerilog(logger);
        });

        return builder;
    }

    internal static HostApplicationBuilder AddServices(this HostApplicationBuilder builder)
    {        
        ArgumentNullException.ThrowIfNull(builder);

        
        builder.Services.AddYamlDeserializer();
        builder.Services.AddSingleton<ILoadUserConfigurationService, LoadUserConfigurationService>();
        builder.Services.AddSingleton<ISingleUserApp, Services.SingleUserApp>();
        return builder;
    }
    
}