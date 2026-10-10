using DirsBooking.Api.Dtos;
using DirsBooking.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DirsBooking.Api.Controllers;

[ApiController]
[Route("api/slots")]
public class SlotsController(SlotService slots) : ControllerBase
{
    /// <summary>Free future slots, optionally for one trainee and a time window.</summary>
    [HttpGet]
    public async Task<ActionResult<List<SlotDto>>> List(int? traineeId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
        => await slots.ListAvailableAsync(traineeId, fromUtc, toUtc, ct);

    /// <summary>A trainee publishes a new free slot (30 min to 4 h, in the future, no overlap).</summary>
    [HttpPost]
    [ProducesResponseType<SlotDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateSlotRequest req, CancellationToken ct)
    {
        var (slot, error) = await slots.CreateAsync(req, ct);
        return error switch
        {
            SlotError.TraineeNotFound => Problem(statusCode: 404, title: "Trainee not found."),
            SlotError.InvalidTimes => Problem(statusCode: 422, title: "A slot must start in the future and last between 30 minutes and 4 hours."),
            SlotError.Overlaps => Problem(statusCode: 409, title: "This slot overlaps another slot of the same trainee."),
            _ => Created($"/api/slots/{slot!.Id}", new SlotDto(slot.Id, slot.TraineeId, slot.Trainee!.FullName, slot.StartsAtUtc, slot.EndsAtUtc, slot.IsBooked))
        };
    }
}
