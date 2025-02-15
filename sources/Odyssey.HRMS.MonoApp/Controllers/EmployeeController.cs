using Microsoft.AspNetCore.Mvc;
using Odyssey.HRMS.MonoApp.Dto.Employee;

namespace Odyssey.HRMS.MonoApp.Controllers;

[Route("api/employees")]
public sealed class EmployeeController: ControllerBase
{
    [HttpGet("{id:required}")]    
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetEmployee([FromRoute] Guid id)
    {
        return Ok(new EmployeeDto(id));
    }

    [HttpPut]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status201Created)]
    public IActionResult CreateEmployee([FromBody] CreateEmployeeRequestDto requestDto)
    {
        return Ok(new EmployeeDto(Guid.NewGuid()));
    }

    [HttpPut("{id:required}")]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult UpdateEmployee([FromRoute] Guid id, [FromBody] UpdateEmployeeRequestDto requestDto)
    {
        return Ok(new EmployeeDto(id));
    }
}