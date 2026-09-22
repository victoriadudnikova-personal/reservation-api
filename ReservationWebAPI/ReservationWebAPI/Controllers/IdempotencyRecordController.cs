using DbConnection;
using DbConnection.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using ReservationWebAPI.Application.Services;

namespace ReservationWebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class IdempotencyRecordController : ControllerBase
    {
        private IdempotencyRecordService _idempotenceRecordService;
        private DatabaseContext _databaseContext;

        public IdempotencyRecordController(IdempotencyRecordService idempotencyRecordService, DatabaseContext databaseContext)
        {
            _databaseContext = databaseContext;
            _idempotenceRecordService = idempotencyRecordService;
        }

        [HttpGet(Name = "Get")]
        public IEnumerable<IdempotencyRecord> Get()
        {
            return _idempotenceRecordService.Get(_databaseContext);
        }
        [HttpGet(Name = "GenerateKey")]
        public Guid GenerateKey()
        {
            return Guid.NewGuid();
        }
    }
}
