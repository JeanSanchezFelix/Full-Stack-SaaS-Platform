# SvelteHybridMVC

A hybrid app that combines the structure of **ASP.NET MVC** with the interactivity of **Svelte**. MVC owns the main routing and Razor views, while Svelte powers focused interactive components.

## Features

- **MVC first**: Razor views control page rendering, with Svelte components added where interactivity is useful.
- **Server-side rendering ready**: Includes an SSR endpoint and selective hydration settings.
- **Tailwind styling**: Tailwind CSS utilities drive the UI instead of Bootstrap-style local classes.
- **Svelte component build**: Vite bundles the Svelte client into `wwwroot/_svelte`.

## Run

```bash
# Restore packages
dotnet restore
npm.cmd install

# Build Tailwind and Svelte assets
npm.cmd run build

# Start the MVC app
dotnet run
```

Then open http://localhost:5000.

## Project Structure

```text
Full-Stack-SaaS-Platform/
|-- Controllers/          # MVC controllers
|-- Views/                # Razor views
|   |-- Shared/_Layout.cshtml
|   |-- Home/
|   `-- Products/
|-- SvelteApp/src/        # Svelte source files and Tailwind input CSS
|-- wwwroot/css/          # Built Tailwind CSS
|-- wwwroot/_svelte/      # Built Svelte assets
|-- Core/                 # Hybrid system settings
`-- Infrastructure/       # Dependency injection extensions
```

## Technologies

- ASP.NET Core 10
- Svelte 5
- Vite
- Tailwind CSS
- Razor runtime compilation
