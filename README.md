# Full-Stack SaaS Platform (_Scooter de la Bahía_)

ASP.NET Core MVC application with Svelte/Vite frontend assets and PostgreSQL data access for the Scooter de la Bahía rental workflow.

The app currently uses Razor views as the server-rendered shell, Svelte as the frontend asset pipeline, and Entity Framework Core mappings for an existing PostgreSQL database named `scooter_de_la_bahia`.

## Project Structure

```text
Full-Stack-SaaS-Platform/
|-- Controllers/
|   `-- HomeController.cs
|-- Core/
|   `-- SvelteOptions.cs
|-- Infrastructure/
|   |-- Data/
|   |   `-- AppDbContext.cs
|   `-- Extensions/
|       `-- SvelteServiceExtensions.cs
|-- Models/
|   |-- Booking.cs
|   |-- Customer.cs
|   |-- Rental.cs
|   `-- Review.cs
|-- schema/
|   `-- client_info.sql
|-- SvelteApp/
|   `-- src/
|       |-- client.js
|       `-- styles/site.css
|-- Views/
|   |-- Home/Index.cshtml
|   |-- Shared/_Layout.cshtml
|   |-- _ViewImports.cshtml
|   `-- _ViewStart.cshtml
|-- wwwroot/
|   |-- _svelte/
|   `-- css/
|-- Program.cs
|-- SvelteHybridMVC.csproj
|-- package.json
|-- vite.config.js
`-- tailwind.config.js
```

## Architecture Overview

* **ASP.NET Core (Backend layer)** handles routing, authentication, APIs, and business logic, serves Razor MVC pages where needed, and acts as the central backend for all SaaS applications.

* **Svelte (Frontend layer)** is used for highly interactive UI components, built with Vite and compiled into `wwwroot`, embedded inside Razor/MVC views or used as standalone frontends, and enables modern, reactive user experiences.

* **Monorepo structure** contains multiple SaaS products in a single repository, shares core infrastructure and services across all applications, keeps each product modular and independently maintainable, and allows common utilities and logic to be reused across apps.

* **Hybrid MVC + SPA approach** uses Razor views to handle base routing and server-rendered pages while Svelte powers dynamic and interactive UI sections, balancing server-side rendering with modern frontend interactivity.

* **API-first backend design** ensures a clear separation between frontend and business logic, with all SaaS modules communicating through consistent APIs, enabling future expansion into mobile apps and external client integrations.

* **Modern UI system** uses Tailwind CSS for utility-first styling and a component-driven frontend architecture built with Svelte.

* **Flexible deployment strategy** allows each SaaS module to scale independently while shared backend services reduce duplication and improve overall system efficiency.

## Database

The database schema is expected to already exist in PostgreSQL. The `AppDbContext` class in `Infrastructure/Data/AppDbContext.cs` contains the Entity Framework Core mappings for the existing tables. The connection string should be configured in the `.env` file.

Mapped tables:
* `customer`
* `booking`
* `rental`
* `review`

Configure the connection string with `.env`:

```env
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=scooter_de_la_bahia;Username=postgres;Password=your-password
```

## Run the Project
```bash
# Restore backend dependencies
dotnet restore

# Install frontend dependencies
npm install

# Build frontend assets
npm run build

# Run the backend server
dotnet run
```

Then open:
```
http://localhost:5000
```

## Run with Docker Compose

1. Copy `.env.docker.example` to `.env`.
2. Set `POSTGRES_PASSWORD` in `.env`.
3. Start everything:

```bash
docker compose up -d --build
```

4. Open:

```text
http://localhost:8080
```

Notes:
* The app container gets `ConnectionStrings__DefaultConnection` automatically from Compose and points to the `db` service.
* `./schema` is mounted to `/docker-entrypoint-initdb.d` so SQL scripts there run only on first DB initialization.

### Coolify

* Use **Docker Compose** as the deployment type.
* Point it to this repo and `docker-compose.yml`.
* Set environment variables from `.env.docker.example` in Coolify (`POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `APP_PORT`).
* Expose port `8080` for the `web` service.

## Technologies
* ASP.NET Core 10
* MVC + Razor Views
* Svelte 5
* Vite
* Tailwind CSS
* PostgreSQL
