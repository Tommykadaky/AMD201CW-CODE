# Gateway service image.
# Build from the REPOSITORY ROOT (the service needs shared/ too):
#   docker build -f services/gateway/Gateway.Api/Dockerfile -t treeo/gateway .
# Usually built by infra/docker-compose.yml or the CI workflow instead.

# ---------- Stage 1: build with the full .NET SDK ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project files first and restore packages.
# Docker caches this layer, so packages are downloaded again only when a .csproj changes.
COPY services/gateway/Gateway.Api/Gateway.Api.csproj services/gateway/Gateway.Api/
RUN dotnet restore services/gateway/Gateway.Api/Gateway.Api.csproj

# Now the source code, then publish a Release build
COPY services/gateway/Gateway.Api/ services/gateway/Gateway.Api/
RUN dotnet publish services/gateway/Gateway.Api/Gateway.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Stage 2: small runtime image (no SDK, no source code) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Run as the built-in non-root user of the .NET images
USER $APP_UID
ENTRYPOINT ["dotnet", "Gateway.Api.dll"]
