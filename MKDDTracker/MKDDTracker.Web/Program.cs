using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Services.Database;
using MKDDTracker.Web;
using MKDDTracker.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// L'application écoute uniquement sur l'ordinateur local.
builder.WebHost.UseUrls("http://127.0.0.1:5080");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// La base réelle appartient à l'utilisateur. Elle est créée vide au premier
// lancement et n'est jamais initialisée à partir d'une base de démonstration.
MkddDatabasePaths.EnsureApplicationDataDirectoryExists();
string? explicitDatabasePath =
    GetOptionValue(
        args,
        "--database");

var databasePath =
    string.IsNullOrWhiteSpace(explicitDatabasePath)
        ? MkddDatabasePaths.RealDatabasePath
        : Path.GetFullPath(explicitDatabasePath);

Console.WriteLine(
    $"Base SQLite utilisée : {databasePath}");

static string? GetOptionValue(
    string[] arguments,
    string optionName)
{
    for (var index = 0;
         index < arguments.Length;
         index++)
    {
        if (!string.Equals(
                arguments[index],
                optionName,
                StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (index + 1 >= arguments.Length ||
            string.IsNullOrWhiteSpace(arguments[index + 1]))
        {
            throw new ArgumentException(
                $"L'option {optionName} nécessite une valeur.");
        }

        return arguments[index + 1];
    }

    return null;
}

builder.Services.AddMkddWebServices(builder.Configuration, databasePath);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbFactory = scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<MkddDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    var databaseInitializer = scope.ServiceProvider
        .GetRequiredService<MkddDatabaseInitializer>();
    await databaseInitializer.InitializeAsync(
        db,
        databasePath,
        DatabaseKind.User);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// Pas de redirection HTTPS : l'application fonctionne localement en HTTP.
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
