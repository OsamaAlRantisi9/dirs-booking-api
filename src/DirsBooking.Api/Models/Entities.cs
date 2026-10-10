namespace DirsBooking.Api.Models;

// A dental student who treats patients under supervision and publishes free time slots.
public class Trainee
{
    public int Id { get; set; }
    public required string FullName { get; set; }
    public required string University { get; set; }
    public int StudyYear { get; set; }

    public List<Slot> Slots { get; set; } = [];
}

public class Patient
{
    public int Id { get; set; }
    public required string FullName { get; set; }
    public required string Phone { get; set; }

    public List<Appointment> Appointments { get; set; } = [];
}

// One bookable time window owned by a trainee.
public class Slot
{
    public int Id { get; set; }
    public int TraineeId { get; set; }
    public Trainee? Trainee { get; set; }

    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }

    // Flipped from false to true by a single conditional UPDATE (see BookingService).
    public bool IsBooked { get; set; }
}

public enum AppointmentStatus
{
    Booked,
    Cancelled
}

public class Appointment
{
    public int Id { get; set; }

    public int SlotId { get; set; }
    public Slot? Slot { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
}
