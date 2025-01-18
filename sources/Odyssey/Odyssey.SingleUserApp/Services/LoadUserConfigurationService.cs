using CSharpFunctionalExtensions;
using Odyssey.Calendar;
using YamlDotNet.Serialization;

namespace Odyssey.SingleUserApp.Services;

public sealed class LoadUserConfigurationService: ILoadUserConfigurationService
{
    private const string ConfigFileName = "user-config.yaml";

    private readonly IDeserializer _yamlDeserializer;

    // ReSharper disable once ConvertToPrimaryConstructor
    public LoadUserConfigurationService(IDeserializer deserializer)
    {
        _yamlDeserializer = deserializer;
    }
    
    public async Task<Result<SingleUserYamlConfig>> LoadConfiguration(CancellationToken token = default)
    {        
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);
        
        if (!Path.Exists(filePath))
        {
            return Result.Failure<SingleUserYamlConfig>($"Configuration file '{ConfigFileName}' not found");
        }

        var yamlString = await File.ReadAllTextAsync(filePath, token);
        var config = _yamlDeserializer.Deserialize<SingleUserYamlConfig>(yamlString);
        
        return config;
    }
}