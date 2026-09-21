namespace ReservationWebAPI.Application.DTOs
{
    public class OperationRequest
    {
        public enum OperationTypeEnum
        {
            NotDefined = 0,
            Book = 1,
            Confirm = 2,
            Cancel = 3
        }
        public string SerializedOperation { get; set; } = null!;
        public OperationTypeEnum OperationType { get; set; } = OperationTypeEnum.NotDefined;
    }
}
