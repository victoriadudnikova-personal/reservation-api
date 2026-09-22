using DbConnection;
using DbConnection.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ReservationWebAPI.Application.DTOs;

namespace ReservationWebAPI.Application.Services
{
    public class IdempotencyRecordService
    {
        public IEnumerable<IdempotencyRecord> Get(DatabaseContext dbContext)
        {
            return dbContext.IdempotencyRecords.AsQueryable();
        }
        public OperationResponse? CheckIfRequestingSameOperation(DatabaseContext dbContext, Guid idempotencyKey, OperationRequest request)
        {
            var existingIdempotencyRecord = dbContext.IdempotencyRecords.AsNoTracking().FirstOrDefault(idr => idr.Key == idempotencyKey);
            if (existingIdempotencyRecord != null)
            {
                object? savedBodyRequest = null;
                if (request.OperationType == IdempotencyRecord.OperationTypeEnum.Book)
                {
                    savedBodyRequest = BookMeetingRoomRequest.Deserialize(existingIdempotencyRecord.RequestHash);
                }
                else if (request.OperationType == IdempotencyRecord.OperationTypeEnum.Confirm || request.OperationType == IdempotencyRecord.OperationTypeEnum.Cancel)
                {
                    savedBodyRequest = UpdateReservationStatusRequest.Deserialize(existingIdempotencyRecord.RequestHash);
                }
                
                if (savedBodyRequest == null)
                {
                    return new OperationResponse()
                    {
                        ResponseMessage = "Cannot process request due to malformed request in the idempotency record.",
                        StatusCode = StatusCodes.Status400BadRequest
                    };
                }
                if (savedBodyRequest.Equals(request))
                {
                    return new OperationResponse()
                    {
                        ResponseMessage = existingIdempotencyRecord.ResponseBody,
                        StatusCode = existingIdempotencyRecord.ResponseStatusCode
                    };
                }
            }
            return null;
        }
    }
}
