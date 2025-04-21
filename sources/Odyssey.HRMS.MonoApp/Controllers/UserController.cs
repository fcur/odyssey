using Microsoft.AspNetCore.Mvc;
using Odyssey.HRMS.MonoApp.Entities.User;

namespace Odyssey.HRMS.MonoApp.Controllers;

[Route("api/users")]
public sealed class UserController : ControllerBase
{
    [HttpGet("{id:required}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetUser([FromRoute] Guid id)
    {
        return Ok(new UserDto(id));
    }

    [HttpPut]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    public IActionResult CreateUser([FromBody] CreateUserRequestDto requestDto)
    {
        return Ok(new UserDto(Guid.NewGuid()));
    }

    [HttpPut("{id:required}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult UpdateUser([FromRoute] Guid id, [FromBody] UpdateUserRequestDto requestDto)
    {
        return Ok(new UserDto(id));
    }
}