using Odyssey.HRMS.MonoApp.Entities.Employee;
using Refit;

namespace Odyssey.HRMS.MonoApp.Clients;

public interface IEmployeeApi
{
    [Get("/api/employees/{id}")]
    Task<EmployeeDto> GetEmployee([Query]Guid id);
    
    [Put("/api/employees/")]
    Task<EmployeeDto> CreateEmployee([Body] CreateEmployeeRequestDto body);
    
    [Put("/api/employees/{id}")]
    Task<EmployeeDto> UpdateEmployee([Query]Guid id, [Body] UpdateEmployeeRequestDto body);
}