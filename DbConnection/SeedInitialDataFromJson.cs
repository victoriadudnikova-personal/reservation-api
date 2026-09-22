using System.Text.Json;

namespace DbConnection
{
    public class SeedInitialDataFromJson<T> where T : new()
    {
        public T? SeedData(string filename)
        {
            var seedDataFolderPath = DbConnectionSetup.GetPathToInitialSeedDataFolder();
            var seedingJsonFile = seedDataFolderPath.GetFiles($"{filename}.json", SearchOption.TopDirectoryOnly);

            if (seedingJsonFile.Length != 1 || !seedingJsonFile[0].Exists)
            {
                throw new Exception(
                    $"Seeding JSON file '{seedDataFolderPath}{Path.DirectorySeparatorChar}{filename}' is missing!");
            }

            using (var r = new StreamReader(seedingJsonFile[0].FullName))
            {
                try
                {
                    var json = r.ReadToEnd();
                    return JsonSerializer.Deserialize<T>(json);
                }
                catch (Exception ex)
                {
                    var errorMessage = ex.InnerException?.Message ?? ex.Message;
                    throw new Exception($"Error parsing JSON seeding data file '{filename}' Detailed error: {errorMessage}.");
                    ;
                }
            }
        }
    }
}
