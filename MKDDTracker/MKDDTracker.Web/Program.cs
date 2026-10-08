using MKDDTracker.Web.Components;
using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Services.SnapshotService;
using MKDDTracker.Scraper.Services.EvolutionService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var databasePath = Path.GetFullPath(
    Path.Combine(
        builder.Environment.ContentRootPath,
        "..",
        "bin",
        "Debug",
        "net9.0",
        "mkddtracker.db"));

if (!File.Exists(databasePath))
{
    throw new FileNotFoundException(
        $"Base MKDD introuvable : {databasePath}");
}

builder.Services.AddDbContextFactory<MkddDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

builder.Services.AddScoped<MkddSnapshotComparisonService>();
builder.Services.AddScoped<MkddPlayerEvolutionService>();
builder.Services.AddScoped<MkddEvolutionReportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
