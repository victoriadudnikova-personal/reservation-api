using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace DbConnection
{
    public static class DbConnectionSetup
    {
        
       
        public static IServiceCollection SetupDatabaseConnectionInjection(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<DatabaseContext>(options => { ConfigureOptionBuilder(options, configuration); });
            services.AddScoped<DbContext, DatabaseContext>();
            return services;
        }

        private static DbContextOptionsBuilder<DatabaseContext> ConfigureOptionBuilder(DbContextOptionsBuilder options, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("ReservationDb") ?? throw new InvalidOperationException("ConnectionStrings:ReservationDb must be configured.");

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
            var initialSeedDataFolder = Path.Combine(AppContext.BaseDirectory, "InitialSeedData");
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
