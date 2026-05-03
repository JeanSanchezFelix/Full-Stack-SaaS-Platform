namespace SvelteHybridMVC.Core;

public class SvelteOptions
{
    public bool EnableSSR { get; set; } = true;
    public HydrationMode HydrationMode { get; set; } = HydrationMode.Selective;
    public bool WatchForChanges { get; set; } = false;
    public string OutputPath { get; set; } = "wwwroot/_svelte";
    public int V8PoolSize { get; set; } = 4;
}

public enum HydrationMode
{
    Full,
    Selective,
    None
}

public interface ISvelteRenderer
{
    Task<string> RenderAsync(string componentName, object? props = null);
}

public class SvelteRenderer : ISvelteRenderer
{
    private readonly SvelteOptions _options;

    public SvelteRenderer(SvelteOptions options)
    {
        _options = options;
    }

    public async Task<string> RenderAsync(string componentName, object? props = null)
    {
        // SSR implementation placeholder
        await Task.CompletedTask;
        return $"<div data-svelte-component=\"{componentName}\"></div>";
    }
}
