# Dirs Booking API (ASP.NET Core 8)

The appointment-booking core of **Dirs**, the platform I co-founded that connects dental students with patients, rebuilt in **C# / ASP.NET Core 8 + Entity Framework Core + SQL Server**.

The production app is Node.js/Express + MongoDB. I rebuilt this part in .NET while learning the stack, because booking is where the interesting engineering is: two patients must never get the same slot, even when they press "Book" at the same moment.

![CI](https://github.com/OsamaAlRantisi9/dirs-booking-api/actions/workflows/ci.yml/badge.svg)

## What it does

| Method | Route | What it does |
|---|---|---|
| `GET` | `/api/trainees` | List dental trainees |
| `POST` | `/api/patients` | Register a patient (one account per phone number) |
| `GET` | `/api/slots?traineeId=&fromUtc=&toUtc=` | Free future slots |
| `POST` | `/api/slots` | A trainee publishes a slot (30 min to 4 h, future, no overlap) |
| `POST` | `/api/appointments` | Book a slot → `201`, or `409` if someone else just took it |
| `GET` | `/api/appointments/{id}` | One appointment |
| `DELETE` | `/api/appointments/{id}` | Cancel and free the slot |
| `GET` | `/api/patients/{id}/appointments` | A patient's appointments |

Errors use the standard **ProblemDetails** format (`application/problem+json`), and request bodies are validated automatically (`[ApiController]` + data annotations → `400`).

## How double booking is prevented

The naive version reads the slot, sees it free, then writes the appointment. Two requests can both read "free" before either writes, and both succeed. This API closes that gap in three layers:

1. **Check and claim in one statement** ([`BookingService`](src/DirsBooking.Api/Services/BookingService.cs)):
   ```sql
   UPDATE Slots SET IsBooked = 1
   WHERE Id = @id AND IsBooked = 0 AND StartsAtUtc > @now
   ```
   (`ExecuteUpdateAsync` in EF Core). Only one request can match `IsBooked = 0`; the others update 0 rows and get `409 Conflict`.
2. **One transaction** around the claim and the appointment insert, so a slot is never marked booked without its appointment (and cancelling frees both together).
3. **A filtered unique index** in the database as the last line of defence: at most one *active* appointment per slot (`UNIQUE (SlotId) WHERE Status = 'Booked'`). Cancelled rows stay for history, which is why it is filtered.

The integration test `Twenty_simultaneous_requests_for_one_slot_produce_exactly_one_booking` fires 20 parallel requests at one slot and asserts exactly one `201` and nineteen `409`s.

## Run it

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download). No database install needed: it uses a local SQLite file by default.

```bash
dotnet run --project src/DirsBooking.Api
```

Open `http://localhost:5080/swagger`. In Development the database is seeded with two trainees, two patients and five days of slots.

To use **SQL Server** instead, set a connection string:

```bash
ConnectionStrings__SqlServer="Server=localhost,1433;Database=DirsBooking;User Id=sa;Password=...;TrustServerCertificate=True" dotnet run --project src/DirsBooking.Api
```

## Tests

```bash
dotnet test
```

11 integration tests run the real API in memory (`WebApplicationFactory<Program>`) against a fresh database per test, so they exercise real SQL, constraints and HTTP status codes, not mocks.

CI ([GitHub Actions](.github/workflows/ci.yml)) runs the suite twice: on SQLite, and on a **SQL Server 2022** container. SQLite serializes writers, so the concurrency test proves much more on SQL Server, where requests really run in parallel under `READ COMMITTED`.

## Project layout

```
src/DirsBooking.Api
  Controllers/   HTTP only: map requests to services, results to status codes
  Services/      business rules (BookingService, SlotService)
  Data/          EF Core DbContext, constraints and indexes, demo seed
  Models/        entities: Trainee, Patient, Slot, Appointment
  Dtos/          request/response records with validation
tests/DirsBooking.Tests
  ApiFactory.cs  in-memory API + throw-away database per test
```

## Known limitations / next steps

- `EnsureCreated()` instead of **EF Core migrations**: fine for a demo, not for evolving a production schema. Next: migrations.
- No authentication yet. Next: JWT bearer auth with patient and trainee roles, so a patient can only cancel their own appointments.
- The slot-overlap check is check-then-insert. Two overlapping slots created by the same trainee at the same instant could both pass; the real fix is a serializable transaction or a lock on the trainee row.
- No front end; Swagger UI is the client.
