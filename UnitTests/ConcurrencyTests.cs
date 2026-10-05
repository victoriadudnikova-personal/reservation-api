using DbConnection;
using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ReservationWebAPI.Application.DTOs;
using ReservationWebAPI.Application.Helpers;
using ReservationWebAPI.Application.Services;
using UnitTests.Helpers;

namespace UnitTests
{
    [TestFixture]
    [NonParallelizable]
    public class ConcurrencyTests
    {
        private DatabaseContext _db = null!;
        private Guid _roomId;
        private IdempotencyRecordService _idempotencyRecordService;
        private ReservationService _reservationService;
        private MeetingRoomService _meetingRoomService;
        private IConfiguration _configuration;

        [OneTimeSetUp]
        public void InitializeDatabase()
        {
            using var db = DatabaseSetup.CreateContext();

            // Creates the test database if missing and applies migrations.
            db.Database.Migrate();
        }

        [SetUp]
        public void Setup()
        {
            _db = DatabaseSetup.CreateContext();
            _roomId = Guid.NewGuid();
            _db.MeetingRooms.Add(new() { Id = _roomId, Name = "Concurrency test" });
            _db.SaveChanges();
            var configurationWrapper = new ConfigurationWrapper();
            _configuration = configurationWrapper.GetConfiguration();
            _idempotencyRecordService = new IdempotencyRecordService();
            _reservationService = new ReservationService(NullLogger<ReservationService>.Instance, _configuration, _idempotencyRecordService);
            _meetingRoomService = new MeetingRoomService(NullLogger<MeetingRoomService>.Instance, _configuration, _idempotencyRecordService, _reservationService);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                _db.IdempotencyRecords.Where(x => x.Reservation!.MeetingRoomId == _roomId).ExecuteDelete();
                _db.Reservations.Where(x => x.MeetingRoomId == _roomId).ExecuteDelete();
                _db.MeetingRooms.Where(x => x.Id == _roomId).ExecuteDelete();
            }
            finally { _db.Dispose(); }
        }
        [TestCase(false)]
        [TestCase(true)]
        public void DeadlockError_RetriesWithFreshContext(bool wrapped)
        {
            DeadlockRetryScenario.Verify(wrapped);
        }

        [Test]
        public void BusinessError_IsNotRetried()
        {
            var attempts = 0;
            Assert.Throws<CustomException>(() => TransactionRetry.Execute<int>(_db, _ =>
            {
                attempts++;
                throw new CustomException("Conflict", CustomException.ExceptionType.InvalidOperation);
            }));
            attempts.Should().Be(1);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task ConcurrentBookings_ReplaySameKeyOrRejectOverlaps(bool sameKey)
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow.AddHours(1);
            var idempotencyKey = Guid.NewGuid();
            var rejections = new ConcurrentQueue<string>();
            _db.Reservations.Any(r => r.MeetingRoomId == _roomId).Should().BeFalse(
                "each concurrency test must start with a new, unreserved room");
            TestContext.Progress.WriteLine(
                $"Assembly: {typeof(ConcurrencyTests).Assembly.Location}; Room: {_roomId}; Start: {startsAt:O}");

            // Race eight requests for the same interval in an isolated room.
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = _roomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };

            using var ready = new CountdownEvent(8);
            using var start = new ManualResetEventSlim();
            var tasks = Enumerable.Range(0, 8).Select(_ => Task.Factory.StartNew(() =>
            {
                using var db = DatabaseSetup.CreateContext();
                ready.Signal();
                start.Wait();
                try
                {
                    var response = _meetingRoomService.Book(sameKey ? idempotencyKey : Guid.NewGuid(), bookingRequest, db);
                    return response;
                }
                catch (CustomException ex) when (!sameKey)
                {
                    rejections.Enqueue(ex.Message);
                    Assert.That(ex.Message, Does.Contain("already reserved"));
                    return null;
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
            try
            {
                ready.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            }
            finally { start.Set(); }
            var responses = await Task.WhenAll(tasks);
            responses.Should().NotBeEmpty();
            using var verification = DatabaseSetup.CreateContext();
            var details = $"room={_roomId}, start={startsAt:O}, " +
                $"persisted reservations={verification.Reservations.Count(r => r.MeetingRoomId == _roomId)}, " +
                $"rejections=[{string.Join(" | ", rejections)}]";
            responses.Count(x => x != null).Should().Be(sameKey ? 8 : 1,
                "the requests race for an initially empty room; diagnostics: {0}", details);
            responses.Where(x => x != null).Select(x => x!.ReservationId).Distinct().Count().Should().Be(1);
            verification.Reservations.Count(x => x.MeetingRoomId == _roomId).Should().Be(1);
            verification.IdempotencyRecords.Count(x => x.Reservation!.MeetingRoomId == _roomId).Should().Be(1);

        }
    }
}
