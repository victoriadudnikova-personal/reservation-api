using DbConnection;
using Microsoft.AspNetCore.Mvc;
using ReservationWebAPI.Application.Helpers;
using ReservationWebAPI.Application.Services;

namespace ReservationWebAPI.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class ReservationController : ControllerBase
    {
        private ReservationService _reservationService;
        private DatabaseContext _dbContext;

        public ReservationController(ReservationService reservationService, DatabaseContext databaseContext)
        {
            _reservationService = reservationService;
            _dbContext = databaseContext;
        }

        [HttpGet]
        public IActionResult Get()
        {
            try 
            {
                var reservations = _reservationService.Get(_dbContext);
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
        public IActionResult Confirm([FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey, Guid id)
        {
            try
            {
                var result = _reservationService.Confirm(id, idempotencyKey, _dbContext);
                return new OkObjectResult(result);
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
        public IActionResult Cancel([FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey, Guid id)
        {
            try
            {
                var result = _reservationService.Cancel(id, idempotencyKey, _dbContext);
                return new OkObjectResult(result);
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
