using DirsBooking.Api.Data;
using DirsBooking.Api.Dtos;
using DirsBooking.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DirsBooking.Api.Services;

public enum SlotError
{
    None,
    TraineeNotFound,
    InvalidTimes,
    Overlaps
}

public class SlotService(AppDbContext db, TimeProvider clock)
{
    public static readonly TimeSpan MinLength = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan MaxLength = TimeSpan.FromHours(4);

    public async Task<(Slot? Slot, SlotError Error)> CreateAsync(CreateSlotRequest req, CancellationToken ct = default)
    {
        var start = req.StartsAtUtc.ToUniversalTime();
        var end = req.EndsAtUtc.ToUniversalTime();
        var length = end - start;

        if (start <= clock.GetUtcNow().UtcDateTime || length < MinLength || length > MaxLength)
            return (null, SlotError.InvalidTimes);

        var trainee = await db.Trainees.FindAsync([req.TraineeId], ct);
        if (trainee is null)
            return (null, SlotError.TraineeNotFound);

        // Two time ranges overlap when each one starts before the other ends.
        var overlaps = await db.Slots.AnyAsync(s =>
            s.TraineeId == req.TraineeId && s.StartsAtUtc < end && start < s.EndsAtUtc, ct);
        if (overlaps)
            return (null, SlotError.Overlaps);

        var slot = new Slot { Trainee = trainee, StartsAtUtc = start, EndsAtUtc = end };
        db.Slots.Add(slot);
        await db.SaveChangesAsync(ct);
        return (slot, SlotError.None);
    }

    public Task<List<SlotDto>> ListAvailableAsync(int? traineeId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var from = fromUtc is { } f && f > now ? f : now;

        var query = db.Slots.AsNoTracking().Where(s => !s.IsBooked && s.StartsAtUtc > from);
        if (traineeId is not null) query = query.Where(s => s.TraineeId == traineeId);
        if (toUtc is not null) query = query.Where(s => s.StartsAtUtc < toUtc);

        return query
            .OrderBy(s => s.StartsAtUtc)
            .Select(s => new SlotDto(s.Id, s.TraineeId, s.Trainee!.FullName, s.StartsAtUtc, s.EndsAtUtc, s.IsBooked))
            .Take(100)
            .ToListAsync(ct);
    }
}
