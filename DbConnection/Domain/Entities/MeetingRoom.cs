using System.ComponentModel.DataAnnotations.Schema;

namespace DbConnection.Domain.Entities
{
    public class MeetingRoom
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public ICollection<Reservation> AllReservations { get; set; } = new List<Reservation>();

        [NotMapped]
        public Reservation? CurrentReservation { get; set; }
    }
}
