using DbConnection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ReservationWebAPI.Application.DTOs;
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
        private IdempotencyRecordService _idempotencyRecordService;
        private ReservationService _reservationService;
        private MeetingRoomService _meetingRoomService;
        private IConfiguration _configuration;
        private List<Guid> _createdReservations;
        private List<Guid> _createdIdempotencyRecords;

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
            _transaction = _db.Database.BeginTransaction();
            var configurationWrapper = new ConfigurationWrapper();
            _configuration = configurationWrapper.GetConfiguration();
            _idempotencyRecordService = new IdempotencyRecordService();
            _reservationService = new ReservationService(NullLogger<ReservationService>.Instance, _configuration, _idempotencyRecordService);
            _meetingRoomService = new MeetingRoomService(NullLogger<MeetingRoomService>.Instance, _configuration, _idempotencyRecordService, _reservationService);
            _createdIdempotencyRecords = new List<Guid>();
            _createdReservations = new List<Guid>();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
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
            var startsAt = DateTime.UtcNow;
            var idempotencyKey = Guid.NewGuid();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = TestData.MeetingRoomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);
            _createdReservations.Add(bookOperationResponse.ReservationId.Value);
            _createdIdempotencyRecords.Add(idempotencyKey);

            // create partial overlapping reservation
            idempotencyKey = Guid.NewGuid();

            var newBookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = bookingRequest.MeetingRoomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt.AddMinutes(-20),
            };

            var newBookOperationResponse = _meetingRoomService.Book(idempotencyKey, newBookingRequest, _db);
            newBookOperationResponse.Should().NotBeNull();
            newBookOperationResponse.ReservationId.Should().BeNull();
            newBookOperationResponse.StatusCode.Should().Be(400);
            _createdIdempotencyRecords.Add(idempotencyKey);

            DeleteReservationAndRelatedIdempotencyRecords(_createdReservations, _createdIdempotencyRecords);
        }

        [Test]
        public void BookIdenticalTime_RejectBooking()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow;
            var idempotencyKey = Guid.NewGuid();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = TestData.MeetingRoomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);
            _createdReservations.Add(bookOperationResponse.ReservationId.Value);
            _createdIdempotencyRecords.Add(idempotencyKey);

            // create identical reservation
            idempotencyKey = Guid.NewGuid();

            var newBookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = bookingRequest.MeetingRoomId,
                ReservationDurationInMinutes = bookingRequest.ReservationDurationInMinutes,
                StartAt = bookingRequest.StartAt,
            };

            var newBookOperationResponse = _meetingRoomService.Book(idempotencyKey, newBookingRequest, _db);
            newBookOperationResponse.Should().NotBeNull();
            newBookOperationResponse.ReservationId.Should().BeNull();
            newBookOperationResponse.StatusCode.Should().Be(400);
            _createdIdempotencyRecords.Add(idempotencyKey);

            DeleteReservationAndRelatedIdempotencyRecords(_createdReservations, _createdIdempotencyRecords);
        }

        [Test]
        public void BookWithinTime_RejectBooking()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow;
            var idempotencyKey = Guid.NewGuid();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = TestData.MeetingRoomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);
            _createdReservations.Add(bookOperationResponse.ReservationId.Value);
            _createdIdempotencyRecords.Add(idempotencyKey);

            // create containg reservation
            idempotencyKey = Guid.NewGuid();

            var newBookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = bookingRequest.MeetingRoomId,
                ReservationDurationInMinutes = 30,
                StartAt = startsAt.AddMinutes(10),
            };

            var newBookOperationResponse = _meetingRoomService.Book(idempotencyKey, newBookingRequest, _db);
            newBookOperationResponse.Should().NotBeNull();
            newBookOperationResponse.ReservationId.Should().BeNull();
            newBookOperationResponse.StatusCode.Should().Be(400);
            _createdIdempotencyRecords.Add(idempotencyKey);

            DeleteReservationAndRelatedIdempotencyRecords(_createdReservations, _createdIdempotencyRecords);
        }

        [Test]
        public void BookAdjacentTime_RejectBooking()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow;
            var idempotencyKey = Guid.NewGuid();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = TestData.MeetingRoomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);
            _createdReservations.Add(bookOperationResponse.ReservationId.Value);
            _createdIdempotencyRecords.Add(idempotencyKey);

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
            _createdIdempotencyRecords.Add(idempotencyKey);

            DeleteReservationAndRelatedIdempotencyRecords(_createdReservations, _createdIdempotencyRecords);
        }

        [Test]
        public void BookRoomWithSameIdempotencyKey_ReturnSameResponse_NoAction()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow;
            var idempotencyKey = Guid.NewGuid();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = TestData.MeetingRoomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);
            _createdReservations.Add(bookOperationResponse.ReservationId.Value);
            _createdIdempotencyRecords.Add(idempotencyKey);

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

            newBookOperationResponse = _meetingRoomService.Book(idempotencyKey, newBookingRequest, _db);
            newBookOperationResponse.Should().NotBeNull();
            newBookOperationResponse.StatusCode.Should().Be(400);

            DeleteReservationAndRelatedIdempotencyRecords(_createdReservations, _createdIdempotencyRecords);
        }

        [Test]
        public void BookRoom_CreateSuccessfulReservation()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow;
            var idempotencyKey = Guid.NewGuid();

            var roomReservations = _meetingRoomService.GetReservations(TestData.MeetingRoomId, _db, startsAt, startsAt.AddMinutes(reservationTimeInMinutes));
            roomReservations.Should().BeEmpty();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = TestData.MeetingRoomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);
            _createdReservations.Add(bookOperationResponse.ReservationId.Value);
            _createdIdempotencyRecords.Add(idempotencyKey);

            roomReservations = _meetingRoomService.GetReservations(TestData.MeetingRoomId, _db, startsAt, startsAt.AddMinutes(reservationTimeInMinutes));
            roomReservations.Should().NotBeEmpty();
            roomReservations.Count().Should().Be(1);
            roomReservations.First().Status.Should().Be(DbConnection.Domain.Entities.Reservation.ReservationStatus.WaitingConfirmation);

            DeleteReservationAndRelatedIdempotencyRecords(_createdReservations, _createdIdempotencyRecords);
        }

        [Test]
        public void BookRoom_RoomIsNotAvailable()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow;
            var idempotencyKey = Guid.NewGuid();

            var availableRooms = _meetingRoomService.GetAvailable(_db, reservationTimeInMinutes, startsAt);
            availableRooms.Should().NotBeEmpty();
            availableRooms.Select(r => r.Id).Should().Contain(TestData.MeetingRoomId);

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = TestData.MeetingRoomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);
            _createdReservations.Add(bookOperationResponse.ReservationId.Value);
            _createdIdempotencyRecords.Add(idempotencyKey);

            availableRooms = _meetingRoomService.GetAvailable(_db, reservationTimeInMinutes, startsAt);
            availableRooms.Select(r => r.Id).Should().NotContain(TestData.MeetingRoomId);

            DeleteReservationAndRelatedIdempotencyRecords(_createdReservations, _createdIdempotencyRecords);
        }

        [Test]
        public void BookConfirmCancel_ReturnCorrectReservationStatus()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow;
            var idempotencyKey = Guid.NewGuid();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = TestData.MeetingRoomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);
            _createdReservations.Add(bookOperationResponse.ReservationId.Value);
            _createdIdempotencyRecords.Add(idempotencyKey);

            var reservation = _db.Reservations.FirstOrDefault(r => r.Id == bookOperationResponse.ReservationId);
            reservation.Should().NotBeNull();
            reservation.Status.Should().Be(DbConnection.Domain.Entities.Reservation.ReservationStatus.WaitingConfirmation);

            idempotencyKey = Guid.NewGuid();
            var confirmOperationResponse = _reservationService.Confirm(reservation.Id, idempotencyKey, _db);
            confirmOperationResponse.Should().NotBeNull();
            confirmOperationResponse.ReservationId.Should().Be(reservation.Id);
            confirmOperationResponse.StatusCode.Should().Be(200);
            _createdIdempotencyRecords.Add(idempotencyKey);

            reservation.Status.Should().Be(DbConnection.Domain.Entities.Reservation.ReservationStatus.Activated);

            idempotencyKey = Guid.NewGuid();
            var cancelOperationResponse = _reservationService.Cancel(reservation.Id, idempotencyKey, _db);
            confirmOperationResponse.Should().NotBeNull();
            confirmOperationResponse.ReservationId.Should().Be(reservation.Id);
            confirmOperationResponse.StatusCode.Should().Be(200);
            _createdIdempotencyRecords.Add(idempotencyKey);

            reservation.Status.Should().Be(DbConnection.Domain.Entities.Reservation.ReservationStatus.Deactivated);

            DeleteReservationAndRelatedIdempotencyRecords(_createdReservations, _createdIdempotencyRecords);
        }

        [Test]
        public void ExpireReservation_CannotConfirm()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow;
            var idempotencyKey = Guid.NewGuid();

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = TestData.MeetingRoomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };
            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
            bookOperationResponse.StatusCode.Should().Be(201);
            _createdReservations.Add(bookOperationResponse.ReservationId.Value);
            _createdIdempotencyRecords.Add(idempotencyKey);

            var reservation = _db.Reservations.FirstOrDefault(r => r.Id == bookOperationResponse.ReservationId);
            reservation.Should().NotBeNull();
            reservation.Status = DbConnection.Domain.Entities.Reservation.ReservationStatus.Deactivated;
            _db.SaveChanges();

            idempotencyKey = Guid.NewGuid();
            var confirmOperationResponse = _reservationService.Confirm(reservation.Id, idempotencyKey, _db);
            confirmOperationResponse.Should().NotBeNull();
            confirmOperationResponse.ReservationId.Should().Be(reservation.Id);
            confirmOperationResponse.StatusCode.Should().Be(400);
            _createdIdempotencyRecords.Add(idempotencyKey);

            DeleteReservationAndRelatedIdempotencyRecords(_createdReservations, _createdIdempotencyRecords);
        }

        private void DeleteReservationAndRelatedIdempotencyRecords(List<Guid> reservationIds, List<Guid> idempotencyRecordKeys)
        {
            _db.IdempotencyRecords.Where(idr => idempotencyRecordKeys.Any(i => i == idr.Key)).ExecuteDelete();
            _db.Reservations.Where(r => reservationIds.Any(i => i == r.Id)).ExecuteDelete();
        }
    }
}
