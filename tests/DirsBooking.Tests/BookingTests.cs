using System.Net;
using System.Net.Http.Json;
using DirsBooking.Api.Dtos;
using DirsBooking.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DirsBooking.Tests;

public class BookingTests
{
    [Fact]
    public async Task Booking_a_free_slot_returns_201_and_marks_the_slot_booked()
    {
        using var api = new ApiFactory();
        var client = api.CreateClient();
        var (_, patientId, slotId) = await api.SeedBasicAsync();

        var res = await client.PostAsJsonAsync("/api/appointments", new BookRequest(slotId, patientId));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var isBooked = await api.WithDbAsync(db => db.Slots.Where(s => s.Id == slotId).Select(s => s.IsBooked).SingleAsync());
        Assert.True(isBooked);
    }

    [Fact]
    public async Task Booking_an_already_booked_slot_returns_409()
    {
        using var api = new ApiFactory();
        var client = api.CreateClient();
        var (_, patientId, slotId) = await api.SeedBasicAsync();

        await client.PostAsJsonAsync("/api/appointments", new BookRequest(slotId, patientId));
        var second = await client.PostAsJsonAsync("/api/appointments", new BookRequest(slotId, patientId));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Twenty_simultaneous_requests_for_one_slot_produce_exactly_one_booking()
    {
        using var api = new ApiFactory();
        var client = api.CreateClient();
        var (_, patientId, slotId) = await api.SeedBasicAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            client.PostAsJsonAsync("/api/appointments", new BookRequest(slotId, patientId))));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(19, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        var active = await api.WithDbAsync(db => db.Appointments.CountAsync(a => a.SlotId == slotId && a.Status == AppointmentStatus.Booked));
        Assert.Equal(1, active);
    }

    [Fact]
    public async Task Cancelling_frees_the_slot_so_it_can_be_booked_again()
    {
        using var api = new ApiFactory();
        var client = api.CreateClient();
        var (_, patientId, slotId) = await api.SeedBasicAsync();

        var booked = await client.PostAsJsonAsync("/api/appointments", new BookRequest(slotId, patientId));
        var appointment = await booked.Content.ReadFromJsonAsync<AppointmentDto>();

        var cancel = await client.DeleteAsync($"/api/appointments/{appointment!.Id}");
        var rebook = await client.PostAsJsonAsync("/api/appointments", new BookRequest(slotId, patientId));

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Created, rebook.StatusCode);
    }

    [Fact]
    public async Task A_slot_that_already_started_cannot_be_booked()
    {
        using var api = new ApiFactory();
        var client = api.CreateClient();
        var (_, patientId, slotId) = await api.SeedBasicAsync(slotStartUtc: DateTime.UtcNow.AddHours(-1));

        var res = await client.PostAsJsonAsync("/api/appointments", new BookRequest(slotId, patientId));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task Unknown_slot_or_patient_returns_404()
    {
        using var api = new ApiFactory();
        var client = api.CreateClient();
        var (_, patientId, slotId) = await api.SeedBasicAsync();

        var noSlot = await client.PostAsJsonAsync("/api/appointments", new BookRequest(999, patientId));
        var noPatient = await client.PostAsJsonAsync("/api/appointments", new BookRequest(slotId, 999));

        Assert.Equal(HttpStatusCode.NotFound, noSlot.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noPatient.StatusCode);
    }
}

public class SlotAndPatientTests
{
    [Fact]
    public async Task Overlapping_slots_for_the_same_trainee_are_rejected()
    {
        using var api = new ApiFactory();
        var client = api.CreateClient();
        var start = DateTime.UtcNow.Date.AddDays(3).AddHours(6);
        var (traineeId, _, _) = await api.SeedBasicAsync(slotStartUtc: start);

        // Existing slot is 06:00-07:00; this one is 06:30-07:30.
        var res = await client.PostAsJsonAsync("/api/slots",
            new CreateSlotRequest(traineeId, start.AddMinutes(30), start.AddMinutes(90)));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task A_back_to_back_slot_is_allowed()
    {
        using var api = new ApiFactory();
        var client = api.CreateClient();
        var start = DateTime.UtcNow.Date.AddDays(3).AddHours(6);
        var (traineeId, _, _) = await api.SeedBasicAsync(slotStartUtc: start);

        // Starts exactly when the existing slot ends.
        var res = await client.PostAsJsonAsync("/api/slots",
            new CreateSlotRequest(traineeId, start.AddHours(1), start.AddHours(2)));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task Booked_slots_are_hidden_from_the_free_slots_list()
    {
        using var api = new ApiFactory();
        var client = api.CreateClient();
        var (traineeId, patientId, slotId) = await api.SeedBasicAsync();

        await client.PostAsJsonAsync("/api/appointments", new BookRequest(slotId, patientId));
        var free = await client.GetFromJsonAsync<List<SlotDto>>($"/api/slots?traineeId={traineeId}");

        Assert.DoesNotContain(free!, s => s.Id == slotId);
    }

    [Fact]
    public async Task The_same_phone_cannot_register_twice()
    {
        using var api = new ApiFactory();
        var client = api.CreateClient();

        var first = await client.PostAsJsonAsync("/api/patients", new CreatePatientRequest("Sara Ali", "+962781234567"));
        var second = await client.PostAsJsonAsync("/api/patients", new CreatePatientRequest("Someone Else", "+962781234567"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task An_invalid_phone_is_rejected_with_400()
    {
        using var api = new ApiFactory();
        var client = api.CreateClient();

        var res = await client.PostAsJsonAsync("/api/patients", new CreatePatientRequest("Sara Ali", "0791234567"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
