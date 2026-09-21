using DbConnection;
using DbConnection.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using ReservationWebAPI.Application.DTOs;
using ReservationWebAPI.Application.Services;

namespace ReservationWebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MeetingRoomController : ControllerBase
    {
        
        private readonly ILogger<MeetingRoomController> _logger;
        private MeetingRoomService _meetingRoomService;
        private DatabaseContext _dbContext;

        public MeetingRoomController(ILogger<MeetingRoomController> logger, MeetingRoomService meetingRoomService, DatabaseContext dbContext)
        {
            _logger = logger;
            _meetingRoomService = meetingRoomService;
            _dbContext = dbContext;
        }

        [HttpGet(Name = "GetAvailable")]
        public IEnumerable<MeetingRoom> GetAvailable(int reservationInMinutes, DateTime? startsAt)
        {
            return _meetingRoomService.GetAvailable(_dbContext, reservationInMinutes, startsAt);
        }

        [HttpPost(Name = "Book")]
        public OperationResponse Book([FromHeader(Name ="Idempotency-Key")]Guid idempotencyKey, BookMeetingRoomRequest request)
        {
            return _meetingRoomService.Book(idempotencyKey, request, _dbContext);
        }

        [HttpGet(Name = "GetReservations")]
        public IEnumerable<Reservation> GetReservations(Guid id, DateTime? startsAt, DateTime? endsAt)
        {
            return _meetingRoomService.GetReservations(id, _dbContext, startsAt, endsAt);
        }

        [HttpGet(Name = "Get")]
        public IEnumerable<MeetingRoom> Get()
        {
            return _meetingRoomService.Get(_dbContext);
        }
    }
}
