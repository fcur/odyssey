using Refit;

namespace Odyssey.HRMS.MonoApp.Dto.Employee;

public interface IEmployeeApi
{
    [Get("/api/employees/{id}")]
    Task<EmployeeDto> GetEmployee([Query]Guid id);
    
    [Put("/api/employees/")]
    Task<EmployeeDto> CreateEmployee([Body] CreateEmployeeRequestDto body);
    
    [Put("/api/employees/{id}")]
    Task<EmployeeDto> UpdateEmployee([Query]Guid id, [Body] UpdateEmployeeRequestDto body);
}