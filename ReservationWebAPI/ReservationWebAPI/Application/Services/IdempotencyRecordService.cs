using Azure.Core;
using DbConnection;
using DbConnection.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ReservationWebAPI.Application.DTOs;
using ReservationWebAPI.Application.Helpers;

namespace ReservationWebAPI.Application.Services
{
    public class IdempotencyRecordService
    {
        public IEnumerable<IdempotencyRecord> Get(DatabaseContext dbContext)
        {
            return dbContext.IdempotencyRecords.AsNoTracking().OrderByDescending(ir => ir.CreatedAt).AsQueryable();
        }
        public OperationResponse? CheckIfRequestingSameOperation(DatabaseContext dbContext, Guid idempotencyKey, OperationRequest request)
        {
            if (idempotencyKey == Guid.Empty)
            {
                throw new CustomException("Idempotency key cannot by empty Guid.", CustomException.ExceptionType.InvalidArgument);
            }

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
                    throw new CustomException("Cannot deserialize the request body from the idempotency record.", CustomException.ExceptionType.InvalidArgument);
                }

                var isSameBody = CheckIfRequestsHaveSameBody(savedBodyRequest, request);

                if (existingIdempotencyRecord.Operation == request.OperationType && isSameBody)
                {
                    return new OperationResponse()
                    {
                        ResponseMessage = existingIdempotencyRecord.ResponseBody,
                        ReservationId = existingIdempotencyRecord.ReservationId,
                        StatusCode = existingIdempotencyRecord.ResponseStatusCode
                    };
                }
                else if (existingIdempotencyRecord.Operation == request.OperationType && !isSameBody)
                {
                    throw new CustomException("Conflict: idempotency keys are identical, but requests are different.", CustomException.ExceptionType.InvalidOperation);
                }
            }
            return null;
        }

        private bool CheckIfRequestsHaveSameBody(object savedBodyRequest, OperationRequest request)
        {
            var isSameBody = savedBodyRequest switch
            {
                BookMeetingRoomRequest saved =>
                    BookMeetingRoomRequest.Deserialize(request.SerializedOperation) is { } incoming &&
                    saved.MeetingRoomId == incoming.MeetingRoomId &&
                    saved.ReservationDurationInMinutes == incoming.ReservationDurationInMinutes &&
                    saved.StartAt == incoming.StartAt,
                UpdateReservationStatusRequest saved =>
                    UpdateReservationStatusRequest.Deserialize(request.SerializedOperation) is { } incoming &&
                    saved.ReservationId == incoming.ReservationId &&
                    saved.Status == incoming.Status,
                _ => false
            };
            return isSameBody;
        }
    }
}
