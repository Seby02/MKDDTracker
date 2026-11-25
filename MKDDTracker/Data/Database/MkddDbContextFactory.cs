using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MKDDTracker.Scraper.Data.Database;

public sealed class MkddDbContextFactory
    : IDesignTimeDbContextFactory<MkddDbContext>
{
    public MkddDbContext CreateDbContext(string[] args)
    {
        string? explicitDatabasePath =
            GetOptionValue(
                args,
                "--database");

        string databasePath;

        if (string.IsNullOrWhiteSpace(explicitDatabasePath))
        {
            MkddDatabasePaths
                .EnsureApplicationDataDirectoryExists();

            databasePath =
                MkddDatabasePaths.RealDatabasePath;
        }
        else
        {
            databasePath =
                Path.GetFullPath(
                    explicitDatabasePath);
        }

        var options =
            new DbContextOptionsBuilder<MkddDbContext>()
                .UseSqlite(
                    $"Data Source={databasePath}")
                .Options;

        return new MkddDbContext(options);
    }

    private static string? GetOptionValue(
        string[] args,
        string optionName)
    {
        for (var index = 0;
             index < args.Length;
             index++)
        {
            if (!string.Equals(
                    args[index],
                    optionName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= args.Length)
            {
                throw new ArgumentException(
                    $"L'option {optionName} nécessite une valeur.");
            }

            string value =
                args[index + 1];

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    $"L'option {optionName} nécessite une valeur.");
            }

            return value;
        }

        return null;
    }
}