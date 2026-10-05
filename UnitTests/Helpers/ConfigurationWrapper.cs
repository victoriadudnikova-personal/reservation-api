using Microsoft.Extensions.Configuration;

namespace UnitTests.Helpers
{
    public class ConfigurationWrapper
    {
        public IConfiguration GetConfiguration()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReservationWaitingConfirmationTimeInMinutes"] = "3",
                ["IdempotencyRecordExpirationTimeInHours"] = "24"
            })
            .Build();
            return configuration;
        }
    }
}
