using DbConnection;
using DbConnection.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ReservationWebAPI.Application.DTOs;

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
            return dbContext.MeetingRooms.OrderBy(mr => mr.Name).AsQueryable();
        }
        public IEnumerable<Reservation> GetReservations(Guid id, DatabaseContext dbContext, DateTime? startsAt, DateTime? endsAt)
        {
            var meetingRoom = dbContext.MeetingRooms.AsNoTracking().FirstOrDefault(mr => mr.Id == id);
            if (meetingRoom == null)
            {
                throw new Exception($"There is not meeting room with id '{id}' in database.");
            }

            var activeReservations = dbContext.Reservations.AsNoTracking().Where(r => r.Id == id && (startsAt.HasValue && r.StartsAtUtc >= startsAt || startsAt == null) && (endsAt.HasValue && r.EndsAtUtc <= endsAt || endsAt == null));

            return activeReservations;
        }
        public IEnumerable<MeetingRoom> GetAvailable(DatabaseContext dbContext, int reservationInMinutes, DateTime startsAt)
        {
            var endsAt = startsAt.AddMinutes(reservationInMinutes);
            var activeReservations = _reservationService.GetAllActiveReservations(dbContext, startsAt, endsAt);
            return dbContext.MeetingRooms.AsNoTracking()
                .Where(mr => !activeReservations.Any(r => r.MeetingRoomId == mr.Id));
        }

        public OperationResponse Book(Guid idempotencyKey, BookMeetingRoomRequest request, DatabaseContext dbContext)
        {
            try
            {
                (var requestHash, var operationResponse) = request.CheckIfCanBeSerialized();
                if (operationResponse != null)
                {
                    return operationResponse;
                }

                var operationRequest = new OperationRequest()
                {
                    OperationType = IdempotencyRecord.OperationTypeEnum.Book,
                    SerializedOperation = requestHash!
                };

                operationResponse = _idempotencyRecordService.CheckIfRequestingSameOperation(dbContext, idempotencyKey, operationRequest);
                if (operationResponse != null)
                {
                    return operationResponse;
                }

                var newIdempotencyRecord = new IdempotencyRecord()
                {
                    Id = Guid.NewGuid(),
                    Key = idempotencyKey,
                    RequestHash = requestHash!,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(_idempotencyRecordExpirationInHours),
                    Operation = IdempotencyRecord.OperationTypeEnum.Book
                };

                operationResponse = CheckIfMeetingRoomExists(dbContext, request, newIdempotencyRecord);
                if (operationResponse != null)
                {
                    return operationResponse;
                }

                var endsAt = request.StartAt.UtcDateTime.AddMinutes(request.ReservationDurationInMinutes);

                operationResponse = CheckIfMeetingRoomAvailable(dbContext, request, newIdempotencyRecord, endsAt);
                if (operationResponse != null)
                {
                    return operationResponse;
                }

                return _reservationService.CreateNewReservationWithIdempotencyRecord(dbContext, request, newIdempotencyRecord, endsAt);
            }
            catch (Exception ex)
            {
                return new OperationResponse
                {
                    ResponseMessage = ex.InnerException?.Message ?? ex.Message,
                    StatusCode = StatusCodes.Status400BadRequest
                };    
            }
        }

        

        private OperationResponse? CheckIfMeetingRoomExists(DatabaseContext dbContext, BookMeetingRoomRequest request, IdempotencyRecord idempotencyRecord) 
        {
            var meetingRoom = dbContext.MeetingRooms.AsNoTracking().FirstOrDefault(mr => mr.Id == request.MeetingRoomId);
            if (meetingRoom == null)
            {
                idempotencyRecord.ResponseBody = $"There is not meeting room with id '{request.MeetingRoomId}' in database.";
                idempotencyRecord.ResponseStatusCode = StatusCodes.Status400BadRequest;
                dbContext.IdempotencyRecords.Add(idempotencyRecord);
                dbContext.SaveChanges();
                return new OperationResponse
                {
                    ResponseMessage = idempotencyRecord.ResponseBody,
                    StatusCode = idempotencyRecord.ResponseStatusCode
                };
            }
            return null;
        }

        private OperationResponse? CheckIfMeetingRoomAvailable(DatabaseContext dbContext, BookMeetingRoomRequest request, IdempotencyRecord idempotencyRecord, DateTime endsAt)
        {
            var activeReservations = _reservationService.GetAllActiveReservations(dbContext, request.StartAt.UtcDateTime, endsAt, request.MeetingRoomId);
            if (activeReservations.Any())
            {
                idempotencyRecord.ResponseBody = $"Meeting room with id '{request.MeetingRoomId}' cannot be booked because it's alerady reserved at this time frame.";
                idempotencyRecord.ResponseStatusCode = StatusCodes.Status400BadRequest;
                dbContext.IdempotencyRecords.Add(idempotencyRecord);
                dbContext.SaveChanges();
                return new OperationResponse
                {
                    ResponseMessage = idempotencyRecord.ResponseBody,
                    StatusCode = idempotencyRecord.ResponseStatusCode
                };
            }
            return null;
        }
    }
}
