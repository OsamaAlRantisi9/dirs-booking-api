using DirsBooking.Api.Data;
using DirsBooking.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace DirsBooking.Tests;

// Starts the real API in memory against its own throw-away database,
// so every test gets a clean database and real SQL (not a fake repository).
// Locally that is a SQLite file; CI also runs the suite on a real SQL Server
// by setting TEST_SQLSERVER (see .github/workflows/ci.yml).
public class ApiFactory : WebApplicationFactory<Program>
{
    private static readonly string? SqlServer = Environment.GetEnvironmentVariable("TEST_SQLSERVER");
    private static bool UseSqlServer => !string.IsNullOrWhiteSpace(SqlServer);

    private readonly string _name = $"dirs_test_{Guid.NewGuid():N}";
    private string SqlitePath => Path.Combine(Path.GetTempPath(), _name + ".db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); // no demo seed data
        builder.UseSetting("ConnectionStrings:SqlServer", UseSqlServer ? $"{SqlServer};Database={_name}" : "");
        builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={SqlitePath}");
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    // One trainee, one patient and one future slot; returns their ids.
    public Task<(int TraineeId, int PatientId, int SlotId)> SeedBasicAsync(DateTime? slotStartUtc = null) =>
        WithDbAsync(async db =>
        {
            var trainee = new Trainee { FullName = "Test Trainee", University = "University of Jordan", StudyYear = 5 };
            var patient = new Patient { FullName = "Test Patient", Phone = "+962790000099" };
            var start = slotStartUtc ?? DateTime.UtcNow.AddDays(1);
            var slot = new Slot { Trainee = trainee, StartsAtUtc = start, EndsAtUtc = start.AddHours(1) };
            db.AddRange(trainee, patient, slot);
            await db.SaveChangesAsync();
            return (trainee.Id, patient.Id, slot.Id);
        });

    // Dispose() runs DisposeAsync(), which stops the host first, so the database
    // must be dropped here, while the app's services are still alive.
    public override async ValueTask DisposeAsync()
    {
        if (UseSqlServer)
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();

        if (!UseSqlServer)
        {
            SqliteConnection.ClearAllPools();
            File.Delete(SqlitePath);
        }
    }
}
