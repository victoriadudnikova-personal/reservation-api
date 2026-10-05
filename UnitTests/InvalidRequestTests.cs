using DbConnection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    public class InvalidRequestTests
    {

        private DatabaseContext _db = null!;
        private IDbContextTransaction? _transaction;
        private IdempotencyRecordService _idempotencyRecordService;
        private ReservationService _reservationService;
        private MeetingRoomService _meetingRoomService;
        private IConfiguration _configuration;
        private Guid _roomId;

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
            _transaction = _db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
            _roomId = Guid.NewGuid();
            _db.MeetingRooms.Add(new() { Id = _roomId, Name = "Reservation test" });
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
                _transaction?.Rollback();
                _db.IdempotencyRecords.Where(x => x.Reservation!.MeetingRoomId == _roomId).ExecuteDelete();
                _db.Reservations.Where(x => x.MeetingRoomId == _roomId).ExecuteDelete();
                _db.MeetingRooms.Where(x => x.Id == _roomId).ExecuteDelete();
            }
            finally
            {
                _transaction?.Dispose();
                _transaction = null;
                _db?.Dispose();
            }
        }

        [Test]
        public void NegativeReservationTime_ThrowException()
        {
            var reservationTimeInMinutes = -120;
            var startsAt = DateTime.UtcNow;
            var idempotencyKey = Guid.NewGuid();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = _roomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            Assert.Throws<CustomException>(() => _meetingRoomService.Book(idempotencyKey, bookingRequest, _db));
        }

        [Test]
        public void GetAvailableWithNegativeReservationTime_ThrowException()
        {
            var reservationTimeInMinutes = -120;
            var startsAt = DateTime.UtcNow;
            
            Assert.Throws<CustomException>(() => _meetingRoomService.GetAvailable(_db, reservationTimeInMinutes, startsAt));
        }

        [Test]
        public void BookRoomInThePast_ThrowException()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow.AddDays(-5);
            var idempotencyKey = Guid.NewGuid();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = _roomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            Assert.Throws<CustomException>(() => _meetingRoomService.Book(idempotencyKey, bookingRequest, _db));
        }

        [Test]
        public void BookWithMissingIdempotencyKey_ThrowException()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow;
            var idempotencyKey = Guid.Empty;

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = _roomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };

            Assert.Throws<CustomException>(() => _meetingRoomService.Book(idempotencyKey, bookingRequest, _db));
        }

        [Test]
        public void GetReservationsOfMissingRoom_ThrowException()
        {
            Assert.Throws<CustomException>(() => _meetingRoomService.GetReservations(Guid.NewGuid(), _db, null, null));
        }

        [Test]
        public void ConfirmUnknownReservation_ThrowException()
        {
            var idempotencyKey = Guid.NewGuid();

            Assert.Throws<CustomException>(() => _reservationService.Confirm(Guid.NewGuid(), idempotencyKey, _db));
        }
    }
}
