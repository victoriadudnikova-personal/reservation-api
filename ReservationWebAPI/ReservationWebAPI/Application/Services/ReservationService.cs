using DbConnection;
using DbConnection.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ReservationWebAPI.Application.DTOs;
namespace ReservationWebAPI.Application.Services
{
    public class ReservationService
    {
        private readonly ILogger<ReservationService> _logger;
        private IdempotencyRecordService _idempotencyRecordService;
        private readonly int _reservationExpirationInMinutes;
        private readonly int _idempotencyRecordExpirationInHours;
        public ReservationService(ILogger<ReservationService> logger, IConfiguration configuration, IdempotencyRecordService idempotencyRecordService)
        {
            _logger = logger;
            _idempotencyRecordService = idempotencyRecordService;
            _reservationExpirationInMinutes = configuration.GetValue<int?>("ReservationWaitingConfirmationTimeInMinutes") ?? 3;
            _idempotencyRecordExpirationInHours = configuration.GetValue<int?>("IdempotencyRecordExpirationTimeInHours") ?? 24;
        }
        public IEnumerable<Reservation> Get(DatabaseContext dbContext)
        {
            return dbContext.Reservations.AsNoTracking().OrderBy(r => r.StartsAtUtc).AsQueryable();
        }

        public OperationResponse Confirm(Guid id, Guid idempotencyKey, DatabaseContext dbContext)
        {
            try
            {
                return UpdateReservationStatus(id, Reservation.ReservationStatus.Activated, idempotencyKey, dbContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.InnerException?.Message ?? ex.Message, ex);
                throw new Exception(ex.Message, ex);
            }
        }
        public OperationResponse Cancel(Guid id, Guid idempotencyKey, DatabaseContext dbContext)
        {
            try
            {
                return UpdateReservationStatus(id, Reservation.ReservationStatus.Deactivated, idempotencyKey, dbContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.InnerException?.Message ?? ex.Message, ex);
                throw new Exception(ex.Message, ex);
            }
        }

        private OperationResponse UpdateReservationStatus(Guid id, Reservation.ReservationStatus newStatus, Guid idempotencyKey, DatabaseContext dbContext)
        {
            var request = new UpdateReservationStatusRequest()
            {
                ReservationId = id,
                Status = newStatus
            };

            (var requestHash, var operationResponse) = request.CheckIfCanBeSerialized();
            if (operationResponse != null)
            {
                return operationResponse;
            }

            var operationRequest = new OperationRequest()
            {
                OperationType = IdempotencyRecord.OperationTypeEnum.Confirm,
                SerializedOperation = requestHash!
            };

            operationResponse = _idempotencyRecordService.CheckIfRequestingSameOperation(dbContext, idempotencyKey, operationRequest);
            if (operationResponse != null)
            {
                return operationResponse;
            }

            return UpdateReservationAndCreateIdempotencyRecord(id, newStatus, idempotencyKey, requestHash!, dbContext);
        }

        private OperationResponse UpdateReservationAndCreateIdempotencyRecord(Guid id, Reservation.ReservationStatus newStatus, Guid idempotencyKey, string requestHash, DatabaseContext dbContext)
        {
            var reservation = dbContext.Reservations.FirstOrDefault(r => r.Id == id);
            if (reservation == null)
            {
                return new OperationResponse
                {
                    ResponseMessage = $"Reservation with id '{id}' is not found in database.",
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }

            if (newStatus == Reservation.ReservationStatus.Activated && reservation.Status != Reservation.ReservationStatus.WaitingConfirmation)
            {
                return new OperationResponse
                {
                    ResponseMessage = $"Reservation with id '{id}' does not have a waiting confirmation status.",
                    ReservationId = reservation.Id,
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }

            var responseMessage = newStatus == Reservation.ReservationStatus.Activated ? "confirmed" : "canceled";
            var idempotencyRecord = new IdempotencyRecord()
            {
                Id = Guid.NewGuid(),
                Key = idempotencyKey,
                RequestHash = requestHash,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(_idempotencyRecordExpirationInHours),
                ReservationId = id,
                ResponseBody = $"Reservation is successfully {responseMessage}.",
                ResponseStatusCode = StatusCodes.Status200OK,
                Operation = newStatus == Reservation.ReservationStatus.Activated ? IdempotencyRecord.OperationTypeEnum.Confirm : IdempotencyRecord.OperationTypeEnum.Cancel
            };
            dbContext.IdempotencyRecords.Add(idempotencyRecord);

            reservation.Status = newStatus;
            dbContext.SaveChanges();
            return new OperationResponse
            {
                ResponseMessage = idempotencyRecord.ResponseBody,
                ReservationId = id,
                StatusCode = StatusCodes.Status200OK
            };
        }

        public IQueryable<Reservation> GetAllActiveReservations(DatabaseContext dbContext, DateTime startsAt, DateTime endsAt, Guid? id = null)
        {

            var activeReservations = dbContext.Reservations.AsNoTracking()
                .Where(r => (r.Status == Reservation.ReservationStatus.Activated ||
                             r.Status == Reservation.ReservationStatus.WaitingConfirmation) &&
                            startsAt < r.EndsAtUtc && r.StartsAtUtc < endsAt &&
                            (id == null || r.MeetingRoomId == id));
            return activeReservations.AsQueryable();
        }

        public OperationResponse CreateNewReservationWithIdempotencyRecord(DatabaseContext dbContext, BookMeetingRoomRequest request, IdempotencyRecord idempotencyRecord, DateTime endsAt)
        {
            var newReservation = new Reservation()
            {
                Id = Guid.NewGuid(),
                MeetingRoomId = request.MeetingRoomId,
                StartsAtUtc = request.StartAt.UtcDateTime,
                EndsAtUtc = endsAt,
                Status = Reservation.ReservationStatus.WaitingConfirmation
            };
            dbContext.Add(newReservation);
            idempotencyRecord.ResponseBody = $"You have {_reservationExpirationInMinutes} minutes to confirm or cancel the reservation. It will be cancelled automatically if no operation is executed.";
            idempotencyRecord.ResponseStatusCode = StatusCodes.Status201Created;
            idempotencyRecord.ReservationId = newReservation.Id;
            dbContext.IdempotencyRecords.Add(idempotencyRecord);

            dbContext.SaveChanges();

            return new OperationResponse()
            {
                ResponseMessage = idempotencyRecord.ResponseBody,
                ReservationId = newReservation.Id,
                StatusCode = idempotencyRecord.ResponseStatusCode
            };
        }
    }
}
