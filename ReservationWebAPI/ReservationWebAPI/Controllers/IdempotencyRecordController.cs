using DbConnection;
using Microsoft.AspNetCore.Mvc;
using ReservationWebAPI.Application.Services;

namespace ReservationWebAPI.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class IdempotencyRecordController : ControllerBase
    {
        private IdempotencyRecordService _idempotenceRecordService;
        private DatabaseContext _databaseContext;

        public IdempotencyRecordController(IdempotencyRecordService idempotencyRecordService, DatabaseContext databaseContext)
        {
            _databaseContext = databaseContext;
            _idempotenceRecordService = idempotencyRecordService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return new OkObjectResult(_idempotenceRecordService.Get(_databaseContext));
        }
        [HttpGet]
        public IActionResult GenerateKey()
        {
            return new OkObjectResult(Guid.NewGuid());
        }
    }
}
