using System.ComponentModel.DataAnnotations;

namespace DirsBooking.Api.Dtos;

// Requests are validated automatically by [ApiController] (400 + ProblemDetails).

public record CreatePatientRequest(
    [Required, StringLength(100, MinimumLength = 2)] string FullName,
    // Jordanian mobile in E.164, e.g. +962791234567
    [Required, RegularExpression(@"^\+9627[789]\d{7}$", ErrorMessage = "Phone must be a Jordanian mobile in +9627XXXXXXXX format.")] string Phone);

public record CreateSlotRequest(
    [Range(1, int.MaxValue)] int TraineeId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc);

public record BookRequest(
    [Range(1, int.MaxValue)] int SlotId,
    [Range(1, int.MaxValue)] int PatientId);

public record TraineeDto(int Id, string FullName, string University, int StudyYear);

public record PatientDto(int Id, string FullName, string Phone);

public record SlotDto(int Id, int TraineeId, string TraineeName, DateTime StartsAtUtc, DateTime EndsAtUtc, bool IsBooked);

public record AppointmentDto(int Id, int SlotId, int PatientId, string Status, DateTime StartsAtUtc, DateTime EndsAtUtc, DateTime CreatedAtUtc);
