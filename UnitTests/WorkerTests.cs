using DbConnection;
using DbConnection.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ReservationWebAPI.Infrastructure.BackgroundServices;
using UnitTests.Helpers;

namespace UnitTests
{
    [TestFixture]
    [NonParallelizable]
    public class WorkerTests
    {
        private IConfiguration _configuration = null!;

        [OneTimeSetUp]
        public void InitializeDatabase()
        {
            using var db = DatabaseSetup.CreateContext();
            db.Database.Migrate();
        }

        [SetUp]
        public void Setup()
        {
            _configuration = new ConfigurationWrapper().GetConfiguration();
        }

        [Test]
        public async Task RunOnceAsync_PreservesFreshBooking_ThenProcessesExpiredBooking()
        {
            var services = new ServiceCollection();
            services.AddScoped<DatabaseContext>(_ => DatabaseSetup.CreateContext());
            await using var provider = services.BuildServiceProvider(validateScopes: true);
            using var worker = new Worker(NullLogger<Worker>.Instance, _configuration, provider.GetRequiredService<IServiceScopeFactory>());

            var now = DateTime.UtcNow;
            var roomId = Guid.NewGuid();
            var reservationId = Guid.NewGuid();
            var idempotencyKey = Guid.NewGuid();
            await using var db = DatabaseSetup.CreateContext();

            try
            {
                db.MeetingRooms.Add(new() { Id = roomId, Name = "Worker test" });
                db.Reservations.Add(new()
                {
                    Id = reservationId,
                    MeetingRoomId = roomId,
                    Status = Reservation.ReservationStatus.WaitingConfirmation,
                    StartsAtUtc = now.AddHours(1),
                    EndsAtUtc = now.AddHours(2)
                });
                db.IdempotencyRecords.Add(new()
                {
                    Key = idempotencyKey,
                    ReservationId = reservationId,
                    Operation = IdempotencyRecord.OperationTypeEnum.Book,
                    RequestHash = "worker-test",
                    ResponseStatusCode = 201,
                    ResponseBody = "{}",
                    CreatedAt = now,
                    ExpiresAt = now.AddHours(24)
                });

                // Commit so the worker's separate context can see these rows.
                await db.SaveChangesAsync();

                await worker.RunOnceAsync(now, CancellationToken.None);

                var reservationStatuses = db.Reservations.AsNoTracking().Where(r => r.Id == reservationId).Select(r => r.Status);
                reservationStatuses.Should().ContainSingle();
                reservationStatuses.Single().Should().Be(Reservation.ReservationStatus.WaitingConfirmation);

                var idempotencyRecords = db.IdempotencyRecords.AsNoTracking().Where(r => r.Key == idempotencyKey);
                idempotencyRecords.Should().ContainSingle();
                idempotencyRecords.Single().Key.Should().Be(idempotencyKey);

                // Advance the clock instead of waiting or changing stored timestamps.
                await worker.RunOnceAsync(now.AddHours(25), CancellationToken.None);

                reservationStatuses = db.Reservations.AsNoTracking().Where(r => r.Id == reservationId).Select(r => r.Status);
                reservationStatuses.Should().ContainSingle();
                reservationStatuses.Single().Should().Be(Reservation.ReservationStatus.Deactivated);

                idempotencyRecords = db.IdempotencyRecords.AsNoTracking().Where(r => r.Key == idempotencyKey);
                idempotencyRecords.Should().BeNullOrEmpty();
            }
            finally
            {
                await db.IdempotencyRecords.Where(r => r.ReservationId == reservationId).ExecuteDeleteAsync();
                await db.Reservations.Where(r => r.Id == reservationId).ExecuteDeleteAsync();
                await db.MeetingRooms.Where(r => r.Id == roomId).ExecuteDeleteAsync();
            }
        }
    }
}
