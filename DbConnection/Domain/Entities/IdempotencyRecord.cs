namespace DbConnection.Domain.Entities
{
    public class IdempotencyRecord
    {
        public enum OperationTypeEnum
        {
            NotDefined = 0,
            Book = 1,
            Confirm = 2,
            Cancel = 3
        }
        public Guid Key { get; set; }
        public OperationTypeEnum Operation { get; set; } = OperationTypeEnum.NotDefined;
        public string RequestHash { get; set; } = null!;
        public int ResponseStatusCode { get; set; }
        public string? ResponseBody { get; set; } = null!;
        public Guid? ReservationId { get; set; }
        public Reservation? Reservation { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
