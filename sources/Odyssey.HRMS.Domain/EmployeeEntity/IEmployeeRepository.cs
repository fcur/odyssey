namespace Odyssey.HRMS.Domain.EmployeeEntity;

public interface IEmployeeRepository
{
    Task<Employee?> Find(EmployeeId id);

    Task Insert(Employee employee);
        
    Task Update(Employee employee);
}