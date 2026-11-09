using MKDDTracker.Web.Components;
using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Services.SnapshotService;
using MKDDTracker.Scraper.Services.EvolutionService;

var builder = WebApplication.CreateBuilder(args);

// L'application écoute uniquement sur l'ordinateur local.
builder.WebHost.UseUrls("http://127.0.0.1:5080");

// Services Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Emplacement de la base initiale intégrée à l'application.
var initialDatabasePath = Path.Combine(
    AppContext.BaseDirectory,
    "Data",
    "mkddtracker.db");

// Emplacement permanent de la base de l'utilisateur Windows.
var appDataDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "MKDDTracker");

Directory.CreateDirectory(appDataDirectory);

var databasePath = Path.Combine(
    appDataDirectory,
    "mkddtracker.db");

// Création de la base utilisateur uniquement au premier lancement.
if (!File.Exists(databasePath))
{
    if (!File.Exists(initialDatabasePath))
    {
        throw new FileNotFoundException(
            $"Base initiale introuvable : {initialDatabasePath}");
    }

    File.Copy(initialDatabasePath, databasePath);
}

// Configuration SQLite
builder.Services.AddDbContextFactory<MkddDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

// Services métier
builder.Services.AddScoped<MkddSnapshotComparisonService>();
builder.Services.AddScoped<MkddPlayerEvolutionService>();
builder.Services.AddScoped<MkddEvolutionReportService>();
builder.Services.AddScoped<MkddSnapshotImportService>();

var app = builder.Build();

// Pipeline HTTP
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