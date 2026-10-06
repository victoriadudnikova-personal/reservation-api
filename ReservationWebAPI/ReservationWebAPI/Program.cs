using DbConnection;
using Microsoft.EntityFrameworkCore;
using ReservationWebAPI.Application.Services;
using ReservationWebAPI.Infrastructure.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.SetupDatabaseConnectionInjection(builder.Configuration);
builder.Services.AddHostedService<Worker>();

builder.Services.AddScoped<IdempotencyRecordService>();
builder.Services.AddScoped<MeetingRoomService>();
builder.Services.AddScoped<ReservationService>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Explicit setup command: migrate and seed without starting the server or worker.
if (args.Contains("--initialize-database"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
    await database.Database.MigrateAsync();
    Console.WriteLine("Database migrations and meeting-room seed data applied.");
    await app.DisposeAsync();
    return;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Reservation API v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
