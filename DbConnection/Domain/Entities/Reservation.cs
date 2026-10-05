namespace DbConnection.Domain.Entities
{
    public class Reservation
    {
        public enum ReservationStatus
        {
            NotDefined = 0,
            WaitingConfirmation = 1,
            Activated = 2,
            Deactivated = 3
        }
        public Guid Id { get; set; }
        public ReservationStatus Status { get; set; } = ReservationStatus.NotDefined;
        public DateTime StartsAtUtc { get; set; }
        public DateTime EndsAtUtc { get; set; }
        public Guid MeetingRoomId { get; set; }
        public MeetingRoom MeetingRoom { get; set; } = null!;
        public ICollection<IdempotencyRecord> IdempotencyRecords { get; set; } = new List<IdempotencyRecord>();
    }
}
