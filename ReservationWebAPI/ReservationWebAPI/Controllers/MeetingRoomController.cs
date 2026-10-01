using DbConnection;
using Microsoft.AspNetCore.Mvc;
using ReservationWebAPI.Application.DTOs;
using ReservationWebAPI.Application.Helpers;
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
        public IActionResult GetAvailable(int reservationInMinutes, DateTimeOffset? startsAt)
        {
            var startsAtUtc = startsAt?.UtcDateTime ?? DateTime.UtcNow;
            try
            {
                return new OkObjectResult(_meetingRoomService.GetAvailable(_dbContext, reservationInMinutes, startsAtUtc));
            }
            catch (CustomException ex)
            {
                return new BadRequestObjectResult(ex);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ExceptionHelper.FromException(ex));
            }
        }

        [HttpPost]
        public IActionResult Book([FromHeader(Name ="Idempotency-Key")]Guid idempotencyKey, [FromBody]BookMeetingRoomRequest request)
        {
            try
            {
                return new OkObjectResult(_meetingRoomService.Book(idempotencyKey, request, _dbContext));
            }
            catch (CustomException ex)
            {
                return new BadRequestObjectResult(ex);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ExceptionHelper.FromException(ex));
            }
            
        }

        [HttpGet]
        public IActionResult GetReservations(Guid id, DateTimeOffset? startsAt, DateTimeOffset? endsAt)
        {
            try
            {
                var reservations = _meetingRoomService.GetReservations(id, _dbContext, startsAt?.UtcDateTime, endsAt?.UtcDateTime);
                return new OkObjectResult(reservations);
            }
            catch (CustomException ex)
            {
                return new BadRequestObjectResult(ex);
            }
            catch (Exception ex)
            { 
                return new BadRequestObjectResult(ExceptionHelper.FromException(ex));
            }
        }

        [HttpGet]
        public IActionResult Get()
        {
            try
            {
                return new OkObjectResult(_meetingRoomService.Get(_dbContext));
            }
            catch (CustomException ex)
            {
                return new BadRequestObjectResult(ex);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ExceptionHelper.FromException(ex));
            }
        }
    }
}
