using MKDDTracker.Scraper.Data.Database;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddDatabasePathsTests
{
    [Fact]
    public void RealAndDemoDatabases_HaveDifferentPaths()
    {
        var root = Path.Combine("test-root", "local-app-data");

        var realPath = MkddDatabasePaths.GetRealDatabasePath(root);
        var demoPath = MkddDatabasePaths.GetDemoDatabasePath(root);

        Assert.NotEqual(realPath, demoPath);
        Assert.Equal("mkddtracker.db", Path.GetFileName(realPath));
        Assert.Equal("mkddtracker-demo.db", Path.GetFileName(demoPath));
    }

    [Fact]
    public void RealAndDemoDatabases_StayInsideTheApplicationDirectory()
    {
        var root = Path.Combine("test-root", "local-app-data");
        var expectedDirectory = Path.Combine(root, "MKDDTracker");

        Assert.Equal(
            expectedDirectory,
            Path.GetDirectoryName(
                MkddDatabasePaths.GetRealDatabasePath(root)));
        Assert.Equal(
            expectedDirectory,
            Path.GetDirectoryName(
                MkddDatabasePaths.GetDemoDatabasePath(root)));
    }
}
