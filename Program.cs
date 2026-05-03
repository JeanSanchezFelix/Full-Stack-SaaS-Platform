using SvelteHybridMVC.Core;
using SvelteHybridMVC.Infrastructure.Data;
using SvelteHybridMVC.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

// 1. Configure MVC with Svelte and runtime compilation.
var mvcBuilder = builder.Services.AddControllersWithViews();
mvcBuilder.AddRazorRuntimeCompilation();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

    options.UseNpgsql(connectionString);
});

// 2. Register core Svelte services.
builder.Services.AddSvelteHybrid(options =>
{
    options.EnableSSR = true;
    options.HydrationMode = HydrationMode.Selective;
    options.WatchForChanges = builder.Environment.IsDevelopment();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Svelte SSR endpoint
app.MapGet("/_svelte/ssr/{component}", async (string component, ISvelteRenderer renderer) =>
{
    var result = await renderer.RenderAsync(component);
    return Results.Content(result, "text/html");
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static void LoadDotEnv()
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), ".env");
    if (!File.Exists(path))
    {
        return;
    }

    foreach (var rawLine in File.ReadLines(path))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim();

        if ((value.StartsWith('"') && value.EndsWith('"')) ||
            (value.StartsWith('\'') && value.EndsWith('\'')))
        {
            value = value[1..^1];
        }

        if (Environment.GetEnvironmentVariable(key) is null)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
