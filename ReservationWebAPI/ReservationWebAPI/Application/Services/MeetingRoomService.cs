using DbConnection;
using DbConnection.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ReservationWebAPI.Application.DTOs;
using ReservationWebAPI.Application.Helpers;

namespace ReservationWebAPI.Application.Services
{
    public class MeetingRoomService
    {
        private IdempotencyRecordService _idempotencyRecordService;
        private ReservationService _reservationService;
        private readonly int _idempotencyRecordExpirationInHours;
        public MeetingRoomService(ILogger<MeetingRoomService> logger, IConfiguration configuration, IdempotencyRecordService idempotencyRecordService, ReservationService reservationService) 
        {
            _reservationService = reservationService;
            _idempotencyRecordService = idempotencyRecordService;
            _idempotencyRecordExpirationInHours = configuration.GetValue<int?>("IdempotencyRecordExpirationTimeInHours") ?? 24;
        }
        public IEnumerable<MeetingRoom> Get(DatabaseContext dbContext)
        {
            return dbContext.MeetingRooms.OrderBy(mr => mr.Name);
        }
        public IEnumerable<Reservation> GetReservations(Guid id, DatabaseContext dbContext, DateTime? startsAt, DateTime? endsAt)
        {
            var meetingRoom = dbContext.MeetingRooms.AsNoTracking().FirstOrDefault(mr => mr.Id == id);
            if (meetingRoom == null)
            {
                throw new CustomException($"Meeting room with id '{id}' is not found in database.", CustomException.ExceptionType.ObjectNotFound);
            }

            var activeReservations = dbContext.Reservations.AsNoTracking().Where(r => r.MeetingRoomId == id && (startsAt.HasValue && r.StartsAtUtc >= startsAt || startsAt == null) && (endsAt.HasValue && r.EndsAtUtc <= endsAt || endsAt == null));

            return activeReservations;
        }
        public IEnumerable<MeetingRoom> GetAvailable(DatabaseContext dbContext, int reservationInMinutes, DateTime startsAt)
        {
            if (reservationInMinutes <= 0)
            {
                throw new CustomException("Reservation time should be a positive number that represents minutes.", CustomException.ExceptionType.InvalidArgument);
            }
            var endsAt = startsAt.AddMinutes(reservationInMinutes);
            var activeReservations = _reservationService.GetAllActiveReservations(dbContext, startsAt, endsAt);
            return dbContext.MeetingRooms.AsNoTracking()
                .Where(mr => !activeReservations.Any(r => r.MeetingRoomId == mr.Id));
        }

        public OperationResponse Book(Guid idempotencyKey, BookMeetingRoomRequest request, DatabaseContext dbContext)
        {
            if (request.ReservationDurationInMinutes <= 0)
            {
                throw new CustomException("Reservation time should be a positive number that represents minutes.", CustomException.ExceptionType.InvalidArgument);
            }

            if (request.StartAt.AddMilliseconds(3000) < DateTimeOffset.UtcNow)
            {
                throw new CustomException("Reservation start time cannot be in the past.", CustomException.ExceptionType.InvalidArgument);
            }

            CheckIfMeetingRoomExists(dbContext, request);

            var requestHash = request.CheckIfCanBeSerialized();
            
            var operationRequest = new OperationRequest()
            {
                OperationType = IdempotencyRecord.OperationTypeEnum.Book,
                SerializedOperation = requestHash!
            };

            var operationResponse = _idempotencyRecordService.CheckIfRequestingSameOperation(dbContext, idempotencyKey, operationRequest);
            if (operationResponse != null)
            {
                return operationResponse;
            }

            var endsAt = request.StartAt.UtcDateTime.AddMinutes(request.ReservationDurationInMinutes);
            CheckIfMeetingRoomAvailable(dbContext, request, endsAt);

            var newIdempotencyRecord = new IdempotencyRecord()
            {
                Id = Guid.NewGuid(),
                Key = idempotencyKey,
                RequestHash = requestHash!,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(_idempotencyRecordExpirationInHours),
                Operation = IdempotencyRecord.OperationTypeEnum.Book
            };

            return _reservationService.CreateNewReservationWithIdempotencyRecord(dbContext, request, newIdempotencyRecord, endsAt);
        }

        

        private void CheckIfMeetingRoomExists(DatabaseContext dbContext, BookMeetingRoomRequest request) 
        {
            var meetingRoom = dbContext.MeetingRooms.AsNoTracking().FirstOrDefault(mr => mr.Id == request.MeetingRoomId);
            if (meetingRoom == null)
            {
                throw new CustomException($"There is not meeting room with id '{request.MeetingRoomId}' in database.", CustomException.ExceptionType.ObjectNotFound);
            }
        }
        private void CheckIfMeetingRoomAvailable(DatabaseContext dbContext, BookMeetingRoomRequest request, DateTime endsAt)
        {
            var activeReservations = _reservationService.GetAllActiveReservations(dbContext, request.StartAt.UtcDateTime, endsAt, request.MeetingRoomId);
            if (activeReservations.Any())
            {
                throw new CustomException($"Meeting room with id '{request.MeetingRoomId}' cannot be booked because it's already reserved at this time frame.", CustomException.ExceptionType.InvalidOperation);
            }
        }
    }
}
