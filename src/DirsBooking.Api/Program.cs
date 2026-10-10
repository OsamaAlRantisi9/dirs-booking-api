using System.Text.Json.Serialization;
using DirsBooking.Api.Data;
using DirsBooking.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// SQL Server when a connection string is configured; otherwise a local SQLite file,
// so the project runs on any machine with only the .NET SDK installed.
var sqlServer = builder.Configuration.GetConnectionString("SqlServer");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(sqlServer))
        options.UseSqlServer(sqlServer);
    else
        options.UseSqlite(builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=dirs-booking.db");
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<SlotService>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
    o.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "DirsBooking.Api.xml")));

var app = builder.Build();

// Unhandled exceptions become a 500 ProblemDetails response with no stack trace.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    if (app.Environment.IsDevelopment())
        SeedData.Apply(db, TimeProvider.System);
}

app.MapControllers();
app.Run();

// Lets the test project start the whole API in memory (WebApplicationFactory<Program>).
public partial class Program;
