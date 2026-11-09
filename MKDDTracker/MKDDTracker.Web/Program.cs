using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Services.EvolutionService;
using MKDDTracker.Scraper.Services.SnapshotService;
using MKDDTracker.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// L'application écoute uniquement sur l'ordinateur local.
builder.WebHost.UseUrls("http://127.0.0.1:5080");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// La base réelle appartient à l'utilisateur. Elle est créée vide au premier
// lancement et n'est jamais initialisée à partir d'une base de démonstration.
MkddDatabasePaths.EnsureApplicationDataDirectoryExists();
var databasePath = MkddDatabasePaths.RealDatabasePath;

builder.Services.AddDbContextFactory<MkddDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

builder.Services.AddScoped<MkddSnapshotComparisonService>();
builder.Services.AddScoped<MkddPlayerEvolutionService>();
builder.Services.AddScoped<MkddEvolutionReportService>();
builder.Services.AddScoped<MkddSnapshotImportService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbFactory = scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<MkddDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
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
