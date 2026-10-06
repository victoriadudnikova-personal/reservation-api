using DbConnection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace UnitTests.Helpers
{
    public class DatabaseSetup
    {

        public static DatabaseContext CreateContext()
        {
            var configuration = new ConfigurationWrapper().GetConfiguration();

            var connectionString = configuration.GetConnectionString("ReservationTestDb");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:ReservationTestDb must be configured.");
            }

            var connection = new SqlConnectionStringBuilder(connectionString);

            // These tests create, update, and delete database records.
            if (connection.InitialCatalog != "ReservationDb_Tests")
            {
                throw new InvalidOperationException("Tests must use the ReservationDb_Tests database.");
            }

            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseSqlServer(connection.ConnectionString)
                .Options;

            return new DatabaseContext(options);
        }
    }
}
