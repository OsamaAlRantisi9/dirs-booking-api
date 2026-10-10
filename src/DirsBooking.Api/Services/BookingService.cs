using DirsBooking.Api.Data;
using DirsBooking.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DirsBooking.Api.Services;

public enum BookingError
{
    None,
    PatientNotFound,
    SlotNotFound,
    SlotAlreadyBooked,
    SlotInPast,
    AppointmentNotFound,
    AlreadyCancelled
}

public record BookingResult(Appointment? Appointment, BookingError Error)
{
    public bool Succeeded => Error == BookingError.None;
    public static BookingResult Ok(Appointment a) => new(a, BookingError.None);
    public static BookingResult Fail(BookingError e) => new(null, e);
}

public class BookingService(AppDbContext db, TimeProvider clock)
{
    public async Task<BookingResult> BookAsync(int slotId, int patientId, CancellationToken ct = default)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct))
            return BookingResult.Fail(BookingError.PatientNotFound);

        var now = clock.GetUtcNow().UtcDateTime;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Check and claim in ONE statement:
        //   UPDATE Slots SET IsBooked = 1 WHERE Id = @id AND IsBooked = 0 AND StartsAtUtc > @now
        // Two requests can both *read* the slot as free, but only one UPDATE can match
        // "IsBooked = 0" -- the other one updates 0 rows. A SELECT-then-INSERT would let both through.
        var claimed = await db.Slots
            .Where(s => s.Id == slotId && !s.IsBooked && s.StartsAtUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsBooked, true), ct);

        if (claimed == 0)
        {
            await tx.RollbackAsync(ct);
            return BookingResult.Fail(await WhyNotBookableAsync(slotId, now, ct));
        }

        var appointment = new Appointment
        {
            SlotId = slotId,
            PatientId = patientId,
            CreatedAtUtc = now
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return BookingResult.Ok(appointment);
    }

    public async Task<BookingResult> CancelAsync(int appointmentId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var appointment = await db.Appointments
            .Include(a => a.Slot)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct);

        if (appointment is null)
            return BookingResult.Fail(BookingError.AppointmentNotFound);
        if (appointment.Status == AppointmentStatus.Cancelled)
            return BookingResult.Fail(BookingError.AlreadyCancelled);
        if (appointment.Slot!.StartsAtUtc <= now)
            return BookingResult.Fail(BookingError.SlotInPast);

        // Both rows change together or not at all: a cancelled appointment
        // must never leave its slot marked as booked.
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAtUtc = now;
        appointment.Slot.IsBooked = false;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return BookingResult.Ok(appointment);
    }

    // Only runs after a failed claim, to return a precise error to the client.
    private async Task<BookingError> WhyNotBookableAsync(int slotId, DateTime now, CancellationToken ct)
    {
        var slot = await db.Slots.AsNoTracking().FirstOrDefaultAsync(s => s.Id == slotId, ct);
        if (slot is null) return BookingError.SlotNotFound;
        if (slot.IsBooked) return BookingError.SlotAlreadyBooked;
        return BookingError.SlotInPast;
    }
}
