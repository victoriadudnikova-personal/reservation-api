using DbConnection;
using DbConnection.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using ReservationWebAPI.Application.DTOs;
using ReservationWebAPI.Application.Services;

namespace ReservationWebAPI.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class MeetingRoomController : ControllerBase
    {
        private MeetingRoomService _meetingRoomService;
        private DatabaseContext _dbContext;

        public MeetingRoomController(MeetingRoomService meetingRoomService, DatabaseContext dbContext)
        {
            _meetingRoomService = meetingRoomService;
            _dbContext = dbContext;
        }

        [HttpGet]
        public IEnumerable<MeetingRoom> GetAvailable(int reservationInMinutes, DateTimeOffset? startsAt)
        {
            var startsAtUtc = startsAt?.UtcDateTime ?? DateTime.UtcNow;
            return _meetingRoomService.GetAvailable(_dbContext, reservationInMinutes, startsAtUtc);
        }

        [HttpPost]
        public OperationResponse Book([FromHeader(Name ="Idempotency-Key")]Guid idempotencyKey, [FromBody]BookMeetingRoomRequest request)
        {
            return _meetingRoomService.Book(idempotencyKey, request, _dbContext);
        }

        [HttpGet]
        public IEnumerable<Reservation> GetReservations(Guid id, DateTimeOffset? startsAt, DateTimeOffset? endsAt)
        {
            return _meetingRoomService.GetReservations(id, _dbContext, startsAt?.UtcDateTime, endsAt?.UtcDateTime);
        }

        [HttpGet]
        public IEnumerable<MeetingRoom> Get()
        {
            return _meetingRoomService.Get(_dbContext);
        }
    }
}
