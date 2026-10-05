using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DbConnection;

public static class TransactionRetry
{
    public static T Execute<T>(DatabaseContext context, Func<DatabaseContext, T> operation)
    {
        // An existing transaction belongs to the caller, who must retry its entire unit of work.
        // Never commit it or retry just a portion of that transaction here.
        if (context.Database.CurrentTransaction != null)
        {
            if (context.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
                throw new InvalidOperationException("The caller-owned transaction must use Serializable isolation and retry the entire transaction.");
            return operation(context);
        }

        var strategy = new TransactionExecutionStrategy(context);

        return strategy.Execute(() =>
        {
            // Failed attempts must not leave tracked inserts or stale entity values.
            using var attempt = context.CreateRetryContext();
            using var transaction = attempt.Database.BeginTransaction(IsolationLevel.Serializable);
            var result = operation(attempt);
            transaction.Commit();
            return result;
        });
    }

    private sealed class TransactionExecutionStrategy(DatabaseContext context)
        : SqlServerRetryingExecutionStrategy(context, 5, TimeSpan.FromSeconds(2), new[] { 1205 })
    {
        protected override bool ShouldRetryOn(Exception exception)
        {
            // A non-retrying context can wrap SaveChanges failures in
            // InvalidOperationException -> DbUpdateException -> SqlException.
            // Classify the underlying SQL error, while replaying the whole transaction.
            for (Exception? current = exception; current != null; current = current.InnerException)
                if (current is SqlException sqlException)
                    return base.ShouldRetryOn(sqlException);
            return base.ShouldRetryOn(exception);
        }
    }
}
