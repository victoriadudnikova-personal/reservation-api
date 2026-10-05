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
    public class ReservationTests
    {
        private DatabaseContext _db = null!;
        private IDbContextTransaction? _transaction;
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
                _db.IdempotencyRecords.Where(x => x.Reservation!.MeetingRoomId == _roomId).ExecuteDelete();
                _db.Reservations.Where(x => x.MeetingRoomId == _roomId).ExecuteDelete();
                _db.MeetingRooms.Where(x => x.Id == _roomId).ExecuteDelete();
                _transaction?.Rollback();
            }
            finally
            {
                _transaction?.Dispose();
                _transaction = null;
                _db?.Dispose();
            }
        }

        [Test]
        public void CanConnectToDatabase()
        {
            var databaseName = _db.Database.GetDbConnection().Database;
            databaseName.Should().Be("ReservationDb_Tests");
            _db.Database.CanConnect().Should().BeTrue();
        }

        [Test]
        public void BookPartiallyOverlappingTime_RejectBooking()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow.AddHours(1);
            var idempotencyKey = Guid.NewGuid();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = _roomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);

            // create partial overlapping reservation
            idempotencyKey = Guid.NewGuid();

            var newBookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = bookingRequest.MeetingRoomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt.AddMinutes(-20),
            };

            Assert.Throws<CustomException>(() => _meetingRoomService.Book(idempotencyKey, newBookingRequest, _db))!
                .Message.Should().Contain("already reserved");
        }

        [Test]
        public void BookIdenticalTime_RejectBooking()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow.AddHours(1);
            var idempotencyKey = Guid.NewGuid();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = _roomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);

            // create identical reservation
            idempotencyKey = Guid.NewGuid();

            var newBookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = bookingRequest.MeetingRoomId,
                ReservationDurationInMinutes = bookingRequest.ReservationDurationInMinutes,
                StartAt = bookingRequest.StartAt,
            };

            Assert.Throws<CustomException>(() => _meetingRoomService.Book(idempotencyKey, newBookingRequest, _db))!
                .Message.Should().Contain("already reserved");
        }

        [Test]
        public void BookWithinTime_RejectBooking()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow.AddHours(1);
            var idempotencyKey = Guid.NewGuid();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = _roomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);

            // create containg reservation
            idempotencyKey = Guid.NewGuid();

            var newBookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = bookingRequest.MeetingRoomId,
                ReservationDurationInMinutes = 30,
                StartAt = startsAt.AddMinutes(10),
            };

            Assert.Throws<CustomException>(() => _meetingRoomService.Book(idempotencyKey, newBookingRequest, _db))!
                .Message.Should().Contain("already reserved");
        }

        [Test]
        public void BookAdjacentTime_SuccessfulBooking()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow.AddHours(1);
            var idempotencyKey = Guid.NewGuid();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = _roomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);

            // create adjacent reservation
            idempotencyKey = Guid.NewGuid();

            var newBookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = bookingRequest.MeetingRoomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt.AddMinutes(reservationTimeInMinutes),
            };

            var newBookOperationResponse = _meetingRoomService.Book(idempotencyKey, newBookingRequest, _db);
            newBookOperationResponse.Should().NotBeNull();
            newBookOperationResponse.ReservationId.Should().NotBeNull();
            newBookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            newBookOperationResponse.ReservationId.Should().NotBe(bookOperationResponse.ReservationId.Value);
            newBookOperationResponse.StatusCode.Should().Be(201);
        }

        [Test]
        public void BookRoomWithSameIdempotencyKey_ReturnSameResponse_NoAction()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow.AddHours(1);
            var idempotencyKey = Guid.NewGuid();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = _roomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);

            // try same idempotency key and same body request
            var newBookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            newBookOperationResponse.Should().NotBeNull();
            newBookOperationResponse.ReservationId.Should().Be(bookOperationResponse.ReservationId);
            newBookOperationResponse.StatusCode.Should().Be(bookOperationResponse.StatusCode);
            newBookOperationResponse.ResponseMessage.Should().Be(bookOperationResponse.ResponseMessage);

            var newBookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = bookingRequest.MeetingRoomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt.AddMinutes(reservationTimeInMinutes)
            };

            Assert.Throws<CustomException>(() => _meetingRoomService.Book(idempotencyKey, newBookingRequest, _db))!
                .Message.Should().Contain("requests are different");

        }

        [Test]
        public void BookRoom_CreateSuccessfulReservation()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow.AddHours(1);
            var idempotencyKey = Guid.NewGuid();

            var roomReservations = _meetingRoomService.GetReservations(_roomId, _db, startsAt, startsAt.AddMinutes(reservationTimeInMinutes));
            roomReservations.Should().BeEmpty();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = _roomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);

            roomReservations = _meetingRoomService.GetReservations(_roomId, _db, startsAt, startsAt.AddMinutes(reservationTimeInMinutes));
            roomReservations.Should().NotBeEmpty();
            roomReservations.Count().Should().Be(1);
            roomReservations.First().Status.Should().Be(DbConnection.Domain.Entities.Reservation.ReservationStatus.WaitingConfirmation);

        }

        [Test]
        public void BookRoom_RoomIsNotAvailable()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow.AddHours(1);
            var idempotencyKey = Guid.NewGuid();

            var availableRooms = _meetingRoomService.GetAvailable(_db, reservationTimeInMinutes, startsAt);
            availableRooms.Should().NotBeEmpty();
            availableRooms.Select(r => r.Id).Should().Contain(_roomId);

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = _roomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);

            availableRooms = _meetingRoomService.GetAvailable(_db, reservationTimeInMinutes, startsAt);
            availableRooms.Select(r => r.Id).Should().NotContain(_roomId);
        }

        [Test]
        public void BookConfirmCancel_ReturnCorrectReservationStatus()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow.AddHours(1);
            var idempotencyKey = Guid.NewGuid();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = _roomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);

            var reservation = _db.Reservations.FirstOrDefault(r => r.Id == bookOperationResponse.ReservationId);
            reservation.Should().NotBeNull();
            reservation.Status.Should().Be(DbConnection.Domain.Entities.Reservation.ReservationStatus.WaitingConfirmation);

            idempotencyKey = Guid.NewGuid();
            var confirmOperationResponse = _reservationService.Confirm(reservation.Id, idempotencyKey, _db);
            confirmOperationResponse.Should().NotBeNull();
            confirmOperationResponse.ReservationId.Should().Be(reservation.Id);
            confirmOperationResponse.StatusCode.Should().Be(200);

            reservation.Status.Should().Be(DbConnection.Domain.Entities.Reservation.ReservationStatus.Activated);

            idempotencyKey = Guid.NewGuid();
            var cancelOperationResponse = _reservationService.Cancel(reservation.Id, idempotencyKey, _db);
            confirmOperationResponse.Should().NotBeNull();
            confirmOperationResponse.ReservationId.Should().Be(reservation.Id);
            confirmOperationResponse.StatusCode.Should().Be(200);

            reservation.Status.Should().Be(DbConnection.Domain.Entities.Reservation.ReservationStatus.Deactivated);

        }

        [Test]
        public void ExpireReservation_CannotConfirm()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow.AddHours(1);
            var idempotencyKey = Guid.NewGuid();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = _roomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);
            var reservation = _db.Reservations.FirstOrDefault(r => r.Id == bookOperationResponse.ReservationId);
            reservation.Should().NotBeNull();
            reservation.Status = DbConnection.Domain.Entities.Reservation.ReservationStatus.Deactivated;
            _db.SaveChanges();

            idempotencyKey = Guid.NewGuid();
            Assert.Throws<CustomException>(() => _reservationService.Confirm(reservation.Id, idempotencyKey, _db));
        }
    }
}
