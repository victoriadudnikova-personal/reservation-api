using static DbConnection.Domain.Entities.IdempotencyRecord;

namespace ReservationWebAPI.Application.DTOs
{
    public class OperationRequest
    {
        
        public string SerializedOperation { get; set; } = null!;
        public OperationTypeEnum OperationType { get; set; } = OperationTypeEnum.NotDefined;
    }
}
