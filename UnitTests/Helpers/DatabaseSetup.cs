using DbConnection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Helpers
{
    public class DatabaseSetup
    {
        public static DatabaseContext CreateContext()
        {
            var connection = new SqlConnectionStringBuilder(
                DbConnectionSetup.GetDatabaseConnectionString())
            {
                InitialCatalog = "ReservationDb_Tests"
            };

            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseSqlServer(connection.ConnectionString)
                .Options;

            return new DatabaseContext(options);
        }
    }
}
