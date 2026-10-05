using DbConnection;
using ReservationWebAPI.Application.Services;
using ReservationWebAPI.Infrastructure.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.SetupDatabaseConnectionInjection();
builder.Services.AddHostedService<Worker>();

builder.Services.AddScoped<IdempotencyRecordService>();
builder.Services.AddScoped<MeetingRoomService>();
builder.Services.AddScoped<ReservationService>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

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
