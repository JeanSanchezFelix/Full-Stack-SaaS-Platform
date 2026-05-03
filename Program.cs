using SvelteHybridMVC.Core;
using SvelteHybridMVC.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure MVC with Svelte and runtime compilation.
var mvcBuilder = builder.Services.AddControllersWithViews();
mvcBuilder.AddRazorRuntimeCompilation();

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
