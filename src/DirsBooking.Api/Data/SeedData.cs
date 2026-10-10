using DirsBooking.Api.Models;

namespace DirsBooking.Api.Data;

// Demo data for local development only (made-up people).
public static class SeedData
{
    public static void Apply(AppDbContext db, TimeProvider clock)
    {
        if (db.Trainees.Any()) return;

        var lina = new Trainee { FullName = "Lina Haddad", University = "University of Jordan", StudyYear = 5 };
        var omar = new Trainee { FullName = "Omar Saleh", University = "University of Jordan", StudyYear = 4 };
        db.Trainees.AddRange(lina, omar);

        db.Patients.AddRange(
            new Patient { FullName = "Demo Patient One", Phone = "+962790000001" },
            new Patient { FullName = "Demo Patient Two", Phone = "+962790000002" });

        // Two clinic sessions a day (09:00 and 12:00 Amman time = 06:00 and 09:00 UTC) for the next 5 days.
        var today = clock.GetUtcNow().UtcDateTime.Date;
        for (var day = 1; day <= 5; day++)
        {
            foreach (var trainee in new[] { lina, omar })
            {
                foreach (var hourUtc in new[] { 6, 9 })
                {
                    var start = today.AddDays(day).AddHours(hourUtc);
                    db.Slots.Add(new Slot { Trainee = trainee, StartsAtUtc = start, EndsAtUtc = start.AddHours(2) });
                }
            }
        }

        db.SaveChanges();
    }
}
