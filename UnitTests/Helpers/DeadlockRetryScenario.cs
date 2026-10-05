using DbConnection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace UnitTests.Helpers;

internal static class DeadlockRetryScenario
{
    public static void Verify(bool wrapError = false)
    {
        // A unique table isolates this scenario from reservation data and other tests.
        var table = "[dbo].[DeadlockProbe_" + Guid.NewGuid().ToString("N") + "]";
        using var db = DatabaseSetup.CreateContext(); // No caller-owned transaction.
        db.Database.SetCommandTimeout(20);
        db.Database.ExecuteSqlRaw("CREATE TABLE " + table +
            " (Id int NOT NULL PRIMARY KEY, Value int NOT NULL); INSERT INTO " +
            table + " VALUES (1, 0), (2, 0);");
        using var firstLocksAcquired = new Barrier(2);
        var contexts = new List<DatabaseContext>();
        Task? competitor = null;
        try
        {
            competitor = Task.Factory.StartNew(() =>
            {
                using var other = DatabaseSetup.CreateContext();
                other.Database.SetCommandTimeout(20);
                using var transaction = other.Database.BeginTransaction();
                other.Database.ExecuteSqlRaw("UPDATE " + table +
                    " WITH (ROWLOCK) SET Value = Value + 1 WHERE Id = 2;");
                if (!firstLocksAcquired.SignalAndWait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("The retry transaction did not acquire its first lock.");
                // Set priority in the same batch as the blocked statement.
                other.Database.ExecuteSqlRaw("SET DEADLOCK_PRIORITY HIGH; UPDATE " + table +
                    " WITH (ROWLOCK) SET Value = Value + 1 WHERE Id = 1;");
                transaction.Commit();
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            var result = TransactionRetry.Execute(db, attempt =>
            {
                contexts.Add(attempt);
                attempt.Database.SetCommandTimeout(20);
                // The competitor's HIGH priority makes this transaction the victim.
                attempt.Database.ExecuteSqlRaw("UPDATE " + table +
                    " WITH (ROWLOCK) SET Value = Value + 1 WHERE Id = 1;");
                if (contexts.Count == 1 &&
                    !firstLocksAcquired.SignalAndWait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("The competing transaction did not acquire its first lock.");
                try
                {
                    attempt.Database.ExecuteSqlRaw("SET DEADLOCK_PRIORITY LOW; UPDATE " + table +
                        " WITH (ROWLOCK) SET Value = Value + 1 WHERE Id = 2;");
                }
                catch (SqlException ex) when (wrapError && ex.Number == 1205)
                {
                    throw new InvalidOperationException("Wrapped transient failure",
                        new DbUpdateException("SaveChanges failed", ex));
                }
                return 42;
            });
            competitor.GetAwaiter().GetResult();
            result.Should().Be(42, "The retry should succeed after the deadlock.");
            contexts.Count.Should().Be(2, "There should be two attempts.");
            contexts.Distinct().Count().Should().Be(2, "The two attempts should be distinct contexts.");
            var values = db.Database.SqlQueryRaw<int>("SELECT Value FROM " + table + " ORDER BY Id").ToArray();
            values.Should().Equal(new[] { 2, 2 }, "Only the competitor and the successful retry should persist changes.");
        }
        finally
        {
            // Let the competing transaction finish before removing its table.
            try { competitor?.GetAwaiter().GetResult(); }
            finally { db.Database.ExecuteSqlRaw("DROP TABLE " + table + ";"); }
        }
    }
}
