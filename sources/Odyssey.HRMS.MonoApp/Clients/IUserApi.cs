using Odyssey.HRMS.MonoApp.Entities.User;
using Refit;

namespace Odyssey.HRMS.MonoApp.Clients;

public interface IUserApi
{
    [Get("/api/users/{id}")]
    Task<UserDto> GetUser([Query]Guid id);
    
    [Put("/api/users/")]
    Task<UserDto> CreateUser([Body] CreateUserRequestDto body);
    
    [Put("/api/users/{id}")]
    Task<UserDto> UpdateUser([Query]Guid id, [Body] UpdateUserRequestDto body);
}