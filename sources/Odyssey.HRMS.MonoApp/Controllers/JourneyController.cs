using MediatR;
using Microsoft.AspNetCore.Mvc;
using Odyssey.HRMS.MonoApp.Commands.CreateJourney;
using Odyssey.HRMS.MonoApp.Commands.GetJourneys;
using Odyssey.HRMS.MonoApp.Commands.UpdateJourney;
using Odyssey.HRMS.MonoApp.Entities.Journey;

namespace Odyssey.HRMS.MonoApp.Controllers;

[Route("api/journeys")]
public sealed class JourneyController : ControllerBase
{
    private readonly IMediator _mediator;

    public JourneyController(IMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(mediator);

        _mediator = mediator;
    }

    [HttpGet("{id:required}")]
    [ProducesResponseType<JourneyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJourney([FromRoute] Guid id, CancellationToken ct)
    {
        var requestDto = new GetJourneysRequestDto();
        var command = requestDto.ToGetJourneysCommand();
        var result = await _mediator.Send(command, ct);

        return Ok(new JourneyDto(id));
    }

    [HttpPut]
    [ProducesResponseType<JourneyDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateJourney([FromBody] CreateJourneyRequestDto requestDto, CancellationToken ct)
    {
        var command = requestDto.ToCreateJourneyCommand();
        var result = await _mediator.Send(command, ct);

        return Ok(new JourneyDto(Guid.NewGuid()));
    }

    [HttpPut("{id:required}")]
    [ProducesResponseType<JourneyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateJourney([FromRoute] Guid id, [FromBody] UpdateJourneyRequestDto requestDto, CancellationToken ct)
    {
        var command = requestDto.ToUpdateJourneyCommand();
        var result = await _mediator.Send(command, ct);

        return Ok(new JourneyDto(id));
    }
}