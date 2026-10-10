using DirsBooking.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DirsBooking.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Trainee> Trainees => Set<Trainee>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Slot> Slots => Set<Slot>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        // Every DateTime is stored as UTC and comes back marked as UTC,
        // so the API never returns a time without its "Z".
        c.Properties<DateTime>().HaveConversion<UtcConverter>();
        c.Properties<DateTime?>().HaveConversion<UtcConverter>();
    }

    private class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Trainee>(e =>
        {
            e.Property(t => t.FullName).HasMaxLength(100);
            e.Property(t => t.University).HasMaxLength(100);
            e.ToTable(t => t.HasCheckConstraint("CK_Trainee_StudyYear", "[StudyYear] BETWEEN 1 AND 6"));
        });

        b.Entity<Patient>(e =>
        {
            e.Property(p => p.FullName).HasMaxLength(100);
            e.Property(p => p.Phone).HasMaxLength(20);
            // One account per phone number, same rule as the real app.
            e.HasIndex(p => p.Phone).IsUnique();
        });

        b.Entity<Slot>(e =>
        {
            e.ToTable(t => t.HasCheckConstraint("CK_Slot_EndsAfterStart", "[EndsAtUtc] > [StartsAtUtc]"));
            // The "free slots for a trainee" query filters on these two columns.
            e.HasIndex(s => new { s.TraineeId, s.StartsAtUtc });
        });

        b.Entity<Appointment>(e =>
        {
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

            // Last line of defence against double booking: at most one *active*
            // appointment per slot, enforced by the database itself.
            // Cancelled rows are kept for history, so the index is filtered.
            e.HasIndex(a => a.SlotId)
                .IsUnique()
                .HasFilter("[Status] = 'Booked'");

            e.HasOne(a => a.Slot).WithMany().HasForeignKey(a => a.SlotId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Patient).WithMany(p => p.Appointments).HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
