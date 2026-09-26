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
                    await using (var scope = _serviceScopeFactory.CreateAsyncScope())
                    {
                        var dbContext =
                            scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                        var nextExpirationTime = DateTime.UtcNow;
                        await CancelObsoleteReservations(nextExpirationTime, dbContext, cancellationToken);
                        await CleanIdempotencyRecords(nextExpirationTime, dbContext, cancellationToken);
                    }

                    await Task.Delay(_loopTargetTimeInMs, cancellationToken);
                }
                catch (OperationCanceledException ex) 
                {
                    _currentLogger.LogError(ex.InnerException?.Message ?? ex.Message, ex);
                }
            }
            
        }

        private async Task CleanIdempotencyRecords(DateTime expirationTime, DatabaseContext dbContext, CancellationToken cancellationToken)
        {
            await dbContext.IdempotencyRecords.Where(ir => ir.ExpiresAt <= expirationTime).ExecuteDeleteAsync(cancellationToken);
        }
        private async Task CancelObsoleteReservations(DateTime expirationTime, DatabaseContext dbContext, CancellationToken cancellationToken)
        {
            var expiredIdempotencyRecords = dbContext.IdempotencyRecords.Where(ir => ir.Operation == DbConnection.Domain.Entities.IdempotencyRecord.OperationTypeEnum.Book && ir.CreatedAt.AddMinutes(_reservationExpirationInMinutes) <= expirationTime);
            var obsoleteReservations = dbContext.Reservations.Include(r => r.IdempotencyRecords).Where(r => r.Status == DbConnection.Domain.Entities.Reservation.ReservationStatus.WaitingConfirmation && expiredIdempotencyRecords.Any(ir => ir.ReservationId == r.Id));
            
            await obsoleteReservations.ForEachAsync(r => 
            {
                
                r.Status = DbConnection.Domain.Entities.Reservation.ReservationStatus.Deactivated;
            }, cancellationToken);
            if (obsoleteReservations.Any())
            {
                dbContext.UpdateRange(obsoleteReservations);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
