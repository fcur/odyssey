using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Odyssey.SingleUserApp.Services;

public sealed class SingleUserApp : ISingleUserApp
{
    private readonly ILogger<SingleUserApp> _logger;
    private readonly ILoadUserConfigurationService _configurationService;

    public SingleUserApp(
        ILoadUserConfigurationService configurationService,
        IConfiguration configuration,
        ILogger<SingleUserApp> logger)
    {
        ArgumentNullException.ThrowIfNull(configurationService);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        
        var result = configuration.GetValue<string>("CustomKey");
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task Start(CancellationToken token = default)
    {
        _logger.LogInformation("Starting...");
        var configuration = await _configurationService.LoadConfiguration(token);
        await Task.Delay(10000, token);
        _logger.LogInformation("Finished...");

    }
}