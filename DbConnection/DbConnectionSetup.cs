using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace DbConnection
{
    public static class DbConnectionSetup
    {
        private static readonly string DatabaseConnectionStringTemplate =
            "Server=%SERVER%,%PORT%;database=%DATABASE%;uid=sa;pwd=Agh!8Ds?qL7r2e3u;encrypt=yes;TrustServerCertificate=True";

        private static readonly int SQLServerPort = 1433;
        private static readonly int SQLServerExternalPortForReservationDb = 203;
        private static readonly string LocalDatabase = "ReservationDb";

        private static readonly string DatabaseServerLocally = "127.0.0.1";

        public static DatabaseContext CreateDbContext()
        {
            var connectionString = GetDatabaseConnectionString();
            var optionBuilder = new DbContextOptionsBuilder<DatabaseContext>()
                .UseSqlServer(connectionString, options => options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            return new DatabaseContext(optionBuilder.Options);
        }

        public static string? GetDatabaseConnectionString()
        {
            var usedPort = SQLServerPort;
            usedPort += SQLServerExternalPortForReservationDb;

            return DatabaseConnectionStringTemplate.Replace("%SERVER%", DatabaseServerLocally).Replace("%DATABASE%", LocalDatabase).Replace("%PORT%", usedPort.ToString());
        }

        public static IServiceCollection SetupDatabaseConnectionInjection(this IServiceCollection services)
        {
            services.AddDbContext<DatabaseContext>(options => { ConfigureOptionBuilder(options); });
            services.AddScoped<DbContext, DatabaseContext>();
            return services;
        }

        private static DbContextOptionsBuilder<DatabaseContext> ConfigureOptionBuilder(DbContextOptionsBuilder options)
        {
            var connectionString = GetDatabaseConnectionString();

            options.UseSqlServer(connectionString
                    ,
                    opt => opt.EnableRetryOnFailure(
                        5,
                        TimeSpan.FromSeconds(10),
                        null))
                .ConfigureWarnings(c => c.Log((RelationalEventId.CommandExecuted, LogLevel.Debug)))
                .ConfigureWarnings(c => c.Log((RelationalEventId.CommandExecuting, LogLevel.Debug)))
                .ConfigureWarnings(c => c.Log((RelationalEventId.CommandCreating, LogLevel.Debug)))
                .ConfigureWarnings(c => c.Log((RelationalEventId.ConnectionOpened, LogLevel.Debug)))
                .ConfigureWarnings(c => c.Log((RelationalEventId.ConnectionOpening, LogLevel.Debug)))
                .ConfigureWarnings(c => c.Log((RelationalEventId.CommandCreated, LogLevel.Debug)))
                .EnableSensitiveDataLogging();

            return (options as DbContextOptionsBuilder<DatabaseContext>)!;
        }

        public static string GetPathToSolutionSource()
        {
            var assembly = Assembly.GetAssembly(typeof(DbConnectionSetup));

            if (assembly is null)
            {
                throw new Exception(
                    $"Can not determine project solution source directory because assembly for type '{nameof(DbConnectionSetup)}' is not found");
            }

            var projectPath = GetPathToProjectForAssembly(assembly);
            DirectoryInfo projectDir = new(projectPath);
            var solutionSourceDir = projectDir.Parent?.Parent;

            if (solutionSourceDir is null)
            {
                throw new Exception(
                    $"Can not determine project pathToProjectFolder because directory ({projectDir.FullName}) does not have parent projectDir");
            }

            return solutionSourceDir.FullName;
        }

        public static DirectoryInfo GetPathToInitialSeedDataFolder()
        {
            var solutionDirectory = GetPathToSolutionSource();
            var initialSeedDataFolder = Path.Combine(
                $"{solutionDirectory}{Path.DirectorySeparatorChar}DbConnection{Path.DirectorySeparatorChar}InitialSeedData");
            if (!Directory.Exists(initialSeedDataFolder))
            {
                throw new Exception(
                    $"Can not find folder with the path '{initialSeedDataFolder}'.");
            }
            return new DirectoryInfo(initialSeedDataFolder);
        }

        private static string GetPathToProjectForAssembly(Assembly assembly)
        {
            var loc = assembly.Location.IndexOf("bin/", StringComparison.Ordinal);

            if (loc < 0)
            {
                loc = assembly.Location.IndexOf("bin\\", StringComparison.Ordinal);
            }

            var pathUntilBin = assembly.Location[..loc];
            var pathToProjectFolder = Path.GetDirectoryName(pathUntilBin);

            if (pathToProjectFolder is null)
            {
                throw new Exception(
                    $"Can not determine project pathToProjectFolder because pathToProjectFolder ({pathToProjectFolder}) could not be calculated from {pathUntilBin}");
            }

            return pathToProjectFolder;
        }
    }
}
