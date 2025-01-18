using CSharpFunctionalExtensions;
using Odyssey.Calendar;

namespace Odyssey.SingleUserApp.Services;

public interface ILoadUserConfigurationService
{
    Task<Result<SingleUserYamlConfig>> LoadConfiguration(CancellationToken token = default);
}