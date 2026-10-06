using Microsoft.Extensions.Configuration;

namespace UnitTests.Helpers
{
    public class ConfigurationWrapper
    {
        public IConfiguration GetConfiguration()
        {
            IConfiguration Configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appSettings.Tests.json", optional: false)
            .AddEnvironmentVariables()
            .Build();
            return Configuration;
        }
    }
}
