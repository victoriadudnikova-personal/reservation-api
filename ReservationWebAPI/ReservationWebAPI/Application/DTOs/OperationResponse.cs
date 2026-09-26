namespace ReservationWebAPI.Application.DTOs
{
    public class OperationResponse
    {
        public string? ResponseMessage { get; set; }
        public Guid? ReservationId { get; set; }
        public int StatusCode { get; set; }
    }
}
