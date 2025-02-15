using CSharpFunctionalExtensions;
using Odyssey.Domain;

namespace Odyssey.SingleUserApp.Services;

public interface ILoadUserConfigurationService
{
    Task<Result<SingleUserYamlConfig>> LoadConfiguration(CancellationToken token = default);
}