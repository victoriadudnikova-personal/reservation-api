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
    public class InvalidRequestTests
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
        public void NegativeReservationTime_RejectBooking()
        {
            var reservationTimeInMinutes = -120;
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
            bookOperationResponse.ReservationId.Should().BeNull();
            bookOperationResponse.StatusCode.Should().Be(400);
            _createdIdempotencyRecords.Add(idempotencyKey);

            DeleteReservationAndRelatedIdempotencyRecords(_createdReservations, _createdIdempotencyRecords);
        }

        [Test]
        public void GetAvailableWithNegativeReservationTime_ThrowException()
        {
            var reservationTimeInMinutes = -120;
            var startsAt = DateTime.UtcNow;
            
            Assert.Throws<ArgumentException>(() => _meetingRoomService.GetAvailable(_db, reservationTimeInMinutes, startsAt));
        }

        [Test]
        public void BookRoomInThePast_RejectBooking()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow.AddDays(-5);
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
            bookOperationResponse.ReservationId.Should().BeNull();
            bookOperationResponse.StatusCode.Should().Be(400);
            _createdIdempotencyRecords.Add(idempotencyKey);

            DeleteReservationAndRelatedIdempotencyRecords(_createdReservations, _createdIdempotencyRecords);
        }

        //TODO: Move to http requests
        //[Test]
        //public void BookWithInvalidIdempotencyKey_RejectBooking()
        //{
        //    var reservationTimeInMinutes = 60;
        //    var startsAt = DateTime.UtcNow;
        //    var idempotencyKey = Guid.NewGuid();

        //    // create one reservation
        //    var bookingRequest = new BookMeetingRoomRequest()
        //    {
        //        MeetingRoomId = TestData.MeetingRoomId,
        //        ReservationDurationInMinutes = reservationTimeInMinutes,
        //        StartAt = startsAt,
        //    };
        //    var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
        //    bookOperationResponse.Should().NotBeNull();
        //    bookOperationResponse.ReservationId.Should().NotBeNull();
        //    bookOperationResponse.ReservationId.Should().NotBe(Guid.Empty);
        //    bookOperationResponse.StatusCode.Should().Be(201);
        //    _createdReservations.Add(bookOperationResponse.ReservationId.Value);
        //    _createdIdempotencyRecords.Add(idempotencyKey);

        //    // create identical reservation
        //    idempotencyKey = Guid.NewGuid();

        //    var newBookingRequest = new BookMeetingRoomRequest()
        //    {
        //        MeetingRoomId = bookingRequest.MeetingRoomId,
        //        ReservationDurationInMinutes = bookingRequest.ReservationDurationInMinutes,
        //        StartAt = bookingRequest.StartAt,
        //    };

        //    var newBookOperationResponse = _meetingRoomService.Book(idempotencyKey, newBookingRequest, _db);
        //    newBookOperationResponse.Should().NotBeNull();
        //    newBookOperationResponse.ReservationId.Should().BeNull();
        //    newBookOperationResponse.StatusCode.Should().Be(400);
        //    _createdIdempotencyRecords.Add(idempotencyKey);

        //    DeleteReservationAndRelatedIdempotencyRecords(_createdReservations, _createdIdempotencyRecords);
        //}

        [Test]
        public void CancelWithMissingIdempotencyKey_Reject()
        {
            var reservationTimeInMinutes = 60;
            var startsAt = DateTime.UtcNow;
            var idempotencyKey = Guid.Empty;

            // create one reservation
            var bookingRequest = new BookMeetingRoomRequest()
            {
                MeetingRoomId = TestData.MeetingRoomId,
                ReservationDurationInMinutes = reservationTimeInMinutes,
                StartAt = startsAt,
            };

            var bookOperationResponse = _meetingRoomService.Book(idempotencyKey, bookingRequest, _db);
            bookOperationResponse.Should().NotBeNull();
            bookOperationResponse.ReservationId.Should().BeNull();
            bookOperationResponse.StatusCode.Should().Be(400);
            _createdIdempotencyRecords.Add(idempotencyKey);

            DeleteReservationAndRelatedIdempotencyRecords(_createdReservations, _createdIdempotencyRecords);
        }

        [Test]
        public void GetReservationsOfMissingRoom_ThrowException()
        {
            Assert.Throws<Exception>(() => _meetingRoomService.GetReservations(Guid.NewGuid(), _db, null, null));
        }

        [Test]
        public void ConfirmUnknownReservation_Reject()
        {
            var idempotencyKey = Guid.NewGuid();

            var confirmOperationResponse = _reservationService.Confirm(Guid.NewGuid(), idempotencyKey, _db);

            confirmOperationResponse.Should().NotBeNull();
            confirmOperationResponse.ReservationId.Should().BeNull();
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
