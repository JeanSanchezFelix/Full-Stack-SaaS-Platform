# Full-Stack SaaS Platform (_Scooter de la Bahía_)

This repository is a **monorepo for multiple SaaS applications**, built using a hybrid architecture that combines **ASP.NET Core (MVC + APIs)** with **Svelte frontend apps**.

Each SaaS product lives inside the same ecosystem, sharing core infrastructure while remaining independently deployable.


## Base Project Structure 
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


## Architecture Overview

* **ASP.NET Core (Backend layer)** handles routing, authentication, APIs, and business logic, serves Razor MVC pages where needed, and acts as the central backend for all SaaS applications.

* **Svelte (Frontend layer)** is used for highly interactive UI components, built with Vite and compiled into `wwwroot`, embedded inside Razor/MVC views or used as standalone frontends, and enables modern, reactive user experiences.

* **Monorepo structure** contains multiple SaaS products in a single repository, shares core infrastructure and services across all applications, keeps each product modular and independently maintainable, and allows common utilities and logic to be reused across apps.

* **Hybrid MVC + SPA approach** uses Razor views to handle base routing and server-rendered pages while Svelte powers dynamic and interactive UI sections, balancing server-side rendering with modern frontend interactivity.

* **API-first backend design** ensures a clear separation between frontend and business logic, with all SaaS modules communicating through consistent APIs, enabling future expansion into mobile apps and external client integrations.

* **Modern UI system** uses Tailwind CSS for utility-first styling and a component-driven frontend architecture built with Svelte.

* **Flexible deployment strategy** allows each SaaS module to scale independently while shared backend services reduce duplication and improve overall system efficiency.


## Run the Project
```bash
# Restore backend dependencies
dotnet restore

# Install frontend dependencies
npm.cmd install

# Build frontend assets
npm.cmd run build

# Run the backend server
dotnet run
```

Then open:
```
http://localhost:5000
```

## Technologies
* ASP.NET Core 10
* MVC + Razor Views
* Svelte 5
* Vite
* Tailwind CSS
* PostgreSQL
