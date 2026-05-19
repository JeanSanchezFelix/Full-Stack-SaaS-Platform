FROM node:20-alpine AS frontend-build
WORKDIR /src

COPY package*.json ./
RUN npm ci

COPY SvelteApp ./SvelteApp
COPY postcss.config.js tailwind.config.js vite.config.js ./
RUN mkdir -p wwwroot/css wwwroot/_svelte
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src

COPY SvelteHybridMVC.csproj ./
RUN dotnet restore

COPY . ./
COPY --from=frontend-build /src/wwwroot ./wwwroot
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=dotnet-build /app/publish ./
ENTRYPOINT ["dotnet", "SvelteHybridMVC.dll"]
