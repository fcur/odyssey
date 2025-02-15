using CSharpFunctionalExtensions;
using Odyssey.HRMS.Domain;

namespace Odyssey.HRMS.SingleUserApp.Services;

public interface ILoadUserConfigurationService
{
    Task<Result<SingleUserYamlConfig>> LoadConfiguration(CancellationToken token = default);
}