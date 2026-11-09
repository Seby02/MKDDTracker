namespace MKDDTracker.Scraper.Data.Database;

public static class MkddDatabasePaths
{
    public const string ApplicationDirectoryName = "MKDDTracker";
    public const string RealDatabaseFileName = "mkddtracker.db";
    public const string DemoDatabaseFileName = "mkddtracker-demo.db";

    public static string ApplicationDataDirectory =>
        GetApplicationDataDirectory();

    public static string RealDatabasePath =>
        GetRealDatabasePath();

    public static string DemoDatabasePath =>
        GetDemoDatabasePath();

    public static string GetApplicationDataDirectory(
        string? localApplicationDataDirectory = null)
    {
        var root = localApplicationDataDirectory
            ?? Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(root, ApplicationDirectoryName);
    }

    public static string GetRealDatabasePath(
        string? localApplicationDataDirectory = null) =>
        Path.Combine(
            GetApplicationDataDirectory(localApplicationDataDirectory),
            RealDatabaseFileName);

    public static string GetDemoDatabasePath(
        string? localApplicationDataDirectory = null) =>
        Path.Combine(
            GetApplicationDataDirectory(localApplicationDataDirectory),
            DemoDatabaseFileName);

    public static void EnsureApplicationDataDirectoryExists() =>
        Directory.CreateDirectory(ApplicationDataDirectory);
}
