using DbConnection;
using DbConnection.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using ReservationWebAPI.Application.DTOs;
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
        public IEnumerable<Reservation> Get()
        {
            return _reservationService.Get(_dbContext);
        }

        [HttpGet]
        public OperationResponse Confirm([FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey, Guid id)
        {
            return _reservationService.Confirm(id, idempotencyKey, _dbContext);
        }

        [HttpGet]
        public OperationResponse Cancel([FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey, Guid id)
        {
            return _reservationService.Cancel(id, idempotencyKey, _dbContext);
        }
    }
}
