# Sensor service image.
# Build from the REPOSITORY ROOT (the service needs shared/ too):
#   docker build -f services/sensor/Sensor.Api/Dockerfile -t treeo/sensor .
# Usually built by infra/docker-compose.yml or the CI workflow instead.

# ---------- Stage 1: build with the full .NET SDK ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project files first and restore packages.
# Docker caches this layer, so packages are downloaded again only when a .csproj changes.
COPY shared/TreeO.Contracts/TreeO.Contracts.csproj shared/TreeO.Contracts/
COPY services/sensor/Sensor.Api/Sensor.Api.csproj services/sensor/Sensor.Api/
RUN dotnet restore services/sensor/Sensor.Api/Sensor.Api.csproj

# Now the source code, then publish a Release build
COPY shared/ shared/
COPY services/sensor/Sensor.Api/ services/sensor/Sensor.Api/
RUN dotnet publish services/sensor/Sensor.Api/Sensor.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Stage 2: small runtime image (no SDK, no source code) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Run as the built-in non-root user of the .NET images
USER $APP_UID
ENTRYPOINT ["dotnet", "Sensor.Api.dll"]
