using DirsBooking.Api.Data;
using DirsBooking.Api.Dtos;
using DirsBooking.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DirsBooking.Api.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController(BookingService booking, AppDbContext db) : ControllerBase
{
    /// <summary>A patient books a free slot. Safe under concurrent requests for the same slot.</summary>
    [HttpPost]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Book(BookRequest req, CancellationToken ct)
    {
        var result = await booking.BookAsync(req.SlotId, req.PatientId, ct);
        if (!result.Succeeded) return ToProblem(result.Error);

        var dto = await ToDtoAsync(result.Appointment!.Id, ct);
        return CreatedAtAction(nameof(Get), new { id = dto!.Id }, dto);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AppointmentDto>> Get(int id, CancellationToken ct)
        => await ToDtoAsync(id, ct) is { } dto ? dto : NotFound();

    /// <summary>Cancels an upcoming appointment and frees its slot.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var result = await booking.CancelAsync(id, ct);
        return result.Succeeded ? NoContent() : ToProblem(result.Error);
    }

    private ObjectResult ToProblem(BookingError error) => error switch
    {
        BookingError.PatientNotFound => Problem(statusCode: 404, title: "Patient not found."),
        BookingError.SlotNotFound => Problem(statusCode: 404, title: "Slot not found."),
        BookingError.AppointmentNotFound => Problem(statusCode: 404, title: "Appointment not found."),
        BookingError.SlotAlreadyBooked => Problem(statusCode: 409, title: "This slot has just been booked by someone else."),
        BookingError.AlreadyCancelled => Problem(statusCode: 409, title: "This appointment is already cancelled."),
        BookingError.SlotInPast => Problem(statusCode: 422, title: "This slot has already started."),
        _ => Problem(statusCode: 500)
    };

    private Task<AppointmentDto?> ToDtoAsync(int id, CancellationToken ct) =>
        db.Appointments.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new AppointmentDto(a.Id, a.SlotId, a.PatientId, a.Status.ToString(),
                a.Slot!.StartsAtUtc, a.Slot.EndsAtUtc, a.CreatedAtUtc))
            .FirstOrDefaultAsync(ct);
}
