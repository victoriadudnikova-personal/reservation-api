using DbConnection.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Diagnostics.Metrics;

namespace DbConnection
{
    public class DatabaseContext : DbContext
    {
        public DbSet<MeetingRoom> MeetingRooms => this.Set<MeetingRoom>();
        public DbSet<Reservation> Reservations => this.Set<Reservation>();
        public DbSet<IdempotencyRecord> IdempotencyRecords => this.Set<IdempotencyRecord>();
        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            this.CreateMeetingRoomsEntityDefinitions(modelBuilder);
            this.CreateReservationsEntityDefinitions(modelBuilder);
            this.CreateIdempotencyRecordsEntityDefinitions(modelBuilder);

            this.ConvertDateTimeToUTC(modelBuilder);
        }

        private void CreateMeetingRoomsEntityDefinitions(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MeetingRoom>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasMaxLength(36).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();

                entity.HasIndex(e => new { e.Id, e.Name}).HasFilter(null);

                var meetingRooms = new SeedInitialDataFromJson<List<MeetingRoom>>().SeedData(nameof(MeetingRoom));
                if (meetingRooms != null)
                {
                    entity.HasData(meetingRooms);
                }
            });
        }
        private void CreateReservationsEntityDefinitions(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Reservation>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasMaxLength(36).IsRequired();
                entity.Property(e => e.Status).HasMaxLength(1).IsRequired();
                entity.Property(e => e.StartsAtUtc).IsRequired();
                entity.Property(e => e.EndsAtUtc).IsRequired();
                entity.Property(e => e.MeetingRoomId).HasMaxLength(36).IsRequired();

                entity.HasIndex(e => new { e.Id, e.StartsAtUtc, e.EndsAtUtc, e.MeetingRoomId, e.Status }).HasFilter(null);

                entity.HasOne(e => e.MeetingRoom).WithMany(mr => mr.AllReservations);
            });
        }
        private void CreateIdempotencyRecordsEntityDefinitions(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IdempotencyRecord>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasMaxLength(36).IsRequired();
                entity.Property(e => e.Key).HasMaxLength(36).IsRequired();
                entity.Property(e => e.Operation).HasMaxLength(1).IsRequired(false);
                entity.Property(e => e.RequestHash).IsRequired();
                entity.Property(e => e.ResponseBody).IsRequired();
                entity.Property(e => e.ResponseStatusCode).HasMaxLength(3).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.ExpiresAt).IsRequired();

                entity.HasIndex(e => new { e.Id, e.Key, e.Operation, e.ReservationId, e.RequestHash, e.ResponseBody, e.ResponseStatusCode, e.ExpiresAt, e.CreatedAt }).HasFilter(null);

                entity.HasOne(e => e.Reservation).WithMany(mr => mr.IdempotencyRecords);
            });
        }

        private void ConvertDateTimeToUTC(ModelBuilder modelBuilder)
        {
            var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
                v => v.ToUniversalTime(),
                v => new DateTime(v.Ticks, DateTimeKind.Utc));

            var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
                v => v.HasValue ? v.Value.ToUniversalTime() : v,
                v => v.HasValue ? new DateTime(v.Value.Ticks, DateTimeKind.Utc) : v);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (entityType.IsKeyless)
                {
                    continue;
                }

                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                    {
                        property.SetValueConverter(dateTimeConverter);
                    }
                    else if (property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(nullableDateTimeConverter);
                    }
                }
            }
        }
    }
}
