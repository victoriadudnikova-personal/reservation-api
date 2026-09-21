using DbConnection;
using DbConnection.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ReservationWebAPI.Application.DTOs;

namespace ReservationWebAPI.Application.Services
{
    public class MeetingRoomService
    {
        private readonly ILogger<MeetingRoomService> _logger;
        private IdempotencyRecordService _idempotencyRecordService;
        private ReservationService _reservationService;
        public MeetingRoomService(ILogger<MeetingRoomService> logger, IdempotencyRecordService idempotencyRecordService, ReservationService reservationService) 
        {
            _logger = logger;
            _reservationService = reservationService;
            _idempotencyRecordService = idempotencyRecordService;
        }
        public IEnumerable<MeetingRoom> Get(DatabaseContext dbContext)
        {
            return dbContext.MeetingRooms.AsQueryable();
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
        public IEnumerable<MeetingRoom> GetAvailable(DatabaseContext dbContext, int reservationInMinutes, DateTime? startsAt)
        {
            startsAt = startsAt ?? DateTime.UtcNow;
            var endsAt = startsAt.Value.AddMinutes(reservationInMinutes);
            var activeReservations = _reservationService.GetAllActiveReservations(dbContext, startsAt.Value, endsAt);
            return dbContext.MeetingRooms.AsNoTracking().Include(mr => mr.AllReservations.Except(activeReservations)).AsQueryable();
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
                    OperationType = OperationRequest.OperationTypeEnum.Book,
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
                    ExpiresAt = DateTime.UtcNow.AddHours(24)
                };

                operationResponse = CheckIfMeetingRoomExists(dbContext, request, newIdempotencyRecord);
                if (operationResponse != null)
                {
                    return operationResponse;
                }

                var endsAt = request.StartAtUtc.AddMinutes(request.ReservationDurationInMinutes);

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
            var activeReservations = _reservationService.GetAllActiveReservations(dbContext, request.StartAtUtc, endsAt, request.MeetingRoomId);
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
