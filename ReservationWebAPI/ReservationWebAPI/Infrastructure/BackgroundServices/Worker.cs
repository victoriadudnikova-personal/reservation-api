using DbConnection;
using Microsoft.EntityFrameworkCore;

namespace ReservationWebAPI.Infrastructure.BackgroundServices
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _currentLogger;
        private readonly int _loopTargetTimeInMs = 3000;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly int _reservationExpirationInMinutes;
        public Worker(ILogger<Worker> logger, IConfiguration configuration, IServiceScopeFactory serviceScopeFactory)
        {
            _currentLogger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _reservationExpirationInMinutes = configuration.GetValue<int?>("ReservationWaitingConfirmationTimeInMinutes") ?? 3;
        }
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested) 
            {
                try
                {
                    await RunOnceAsync(DateTime.UtcNow, cancellationToken);

                    await Task.Delay(_loopTargetTimeInMs, cancellationToken);
                }
                catch (OperationCanceledException ex) 
                {
                    _currentLogger.LogError(ex.InnerException?.Message ?? ex.Message, ex);
                }
            }
            
        }

        //Processes expired reservations and idempotency records once using a UTC timestamp
        public async Task RunOnceAsync(DateTime now, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            // Reservations must be processed before their booking records are deleted.
            await CancelObsoleteReservations(now, dbContext, token);
            await CleanIdempotencyRecords(now, dbContext, token);
        }

        private async Task CleanIdempotencyRecords(DateTime expirationTime, DatabaseContext dbContext, CancellationToken cancellationToken)
        {
            await dbContext.IdempotencyRecords.Where(ir => ir.ExpiresAt <= expirationTime).ExecuteDeleteAsync(cancellationToken);
        }
        private async Task CancelObsoleteReservations(DateTime expirationTime, DatabaseContext dbContext, CancellationToken cancellationToken)
        {
            var expiredIdempotencyRecords = dbContext.IdempotencyRecords.Where(ir => ir.Operation == DbConnection.Domain.Entities.IdempotencyRecord.OperationTypeEnum.Book && ir.CreatedAt.AddMinutes(_reservationExpirationInMinutes) <= expirationTime);
            var obsoleteReservations = dbContext.Reservations.Include(r => r.IdempotencyRecords).Where(r => r.Status == DbConnection.Domain.Entities.Reservation.ReservationStatus.WaitingConfirmation && expiredIdempotencyRecords.Any(ir => ir.ReservationId == r.Id));
            
            // Keep the status predicate in the UPDATE so confirmation cannot be overwritten.
            await obsoleteReservations.ExecuteUpdateAsync(setters => setters.SetProperty(r => r.Status, DbConnection.Domain.Entities.Reservation.ReservationStatus.Deactivated),
                cancellationToken);
        }
    }
}
