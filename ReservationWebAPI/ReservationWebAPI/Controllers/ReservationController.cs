using DbConnection;
using DbConnection.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using ReservationWebAPI.Application.DTOs;
using ReservationWebAPI.Application.Services;

namespace ReservationWebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ReservationController : ControllerBase
    {
        
        private readonly ILogger<ReservationController> _logger;
        private ReservationService _reservationService;
        private DatabaseContext _dbContext;

        public ReservationController(ILogger<ReservationController> logger, ReservationService reservationService, DatabaseContext databaseContext)
        {
            _logger = logger;
            _reservationService = reservationService;
            _dbContext = databaseContext;
        }

        [HttpGet(Name = "Get")]
        public IEnumerable<Reservation> Get()
        {
            return _reservationService.Get(_dbContext);
        }

        [HttpGet(Name = "Confirm")]
        public OperationResponse Confirm([FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey, Guid id)
        {
            return _reservationService.Confirm(id, idempotencyKey, _dbContext);
        }

        [HttpGet(Name = "Cancel")]
        public OperationResponse Cancel([FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey, Guid id)
        {
            return _reservationService.Cancel(id, idempotencyKey, _dbContext);
        }
    }
}
