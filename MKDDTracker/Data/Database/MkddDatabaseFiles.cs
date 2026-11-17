namespace MKDDTracker.Scraper.Data.Database;

public static class MkddDatabaseFiles
{
    public static void Delete(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        foreach (var path in new[]
                 {
                     databasePath,
                     $"{databasePath}-shm",
                     $"{databasePath}-wal",
                     $"{databasePath}-journal"
                 })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
