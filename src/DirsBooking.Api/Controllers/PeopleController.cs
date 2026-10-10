using DirsBooking.Api.Data;
using DirsBooking.Api.Dtos;
using DirsBooking.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DirsBooking.Api.Controllers;

[ApiController]
[Route("api")]
public class PeopleController(AppDbContext db) : ControllerBase
{
    [HttpGet("trainees")]
    public Task<List<TraineeDto>> Trainees(CancellationToken ct) =>
        db.Trainees.AsNoTracking()
            .OrderBy(t => t.FullName)
            .Select(t => new TraineeDto(t.Id, t.FullName, t.University, t.StudyYear))
            .ToListAsync(ct);

    /// <summary>Registers a patient. One account per phone number.</summary>
    [HttpPost("patients")]
    [ProducesResponseType<PatientDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePatient(CreatePatientRequest req, CancellationToken ct)
    {
        var patient = new Patient { FullName = req.FullName.Trim(), Phone = req.Phone };
        db.Patients.Add(patient);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The unique index on Phone decides, even when two sign-ups race each other.
            return Problem(statusCode: 409, title: "An account with this phone number already exists.");
        }
        return Created($"/api/patients/{patient.Id}", new PatientDto(patient.Id, patient.FullName, patient.Phone));
    }

    /// <summary>A patient's appointments, newest first.</summary>
    [HttpGet("patients/{id:int}/appointments")]
    public async Task<ActionResult<List<AppointmentDto>>> PatientAppointments(int id, CancellationToken ct)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == id, ct)) return NotFound();

        return await db.Appointments.AsNoTracking()
            .Where(a => a.PatientId == id)
            .OrderByDescending(a => a.Slot!.StartsAtUtc)
            .Select(a => new AppointmentDto(a.Id, a.SlotId, a.PatientId, a.Status.ToString(),
                a.Slot!.StartsAtUtc, a.Slot.EndsAtUtc, a.CreatedAtUtc))
            .ToListAsync(ct);
    }
}
