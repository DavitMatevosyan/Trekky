# Trekky API image. Build context: backend/. Builds natively on the ARM64 VM (ADR-0004) and on x64.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props Trekky.slnx ./
COPY src/ src/
RUN dotnet restore src/Trekky.Api/Trekky.Api.csproj
RUN dotnet publish src/Trekky.Api/Trekky.Api.csproj --no-restore -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
# Owned by `app`, so the data-protection volume mounted here (deploy/docker-compose.yml) is writable.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
# The base image runs as the non-root `app` user and listens on 8080.
USER app
ENTRYPOINT ["dotnet", "Trekky.Api.dll"]
