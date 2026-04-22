# Multi-stage Dockerfile for the AlphaScope API server.
# Stage 1: build + publish with the .NET 8 SDK.
# Stage 2: slim aspnet runtime image carrying only the published app.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file first so the restore layer stays cached across source edits.
COPY AlphaScopeServer/AlphaScopeServer.csproj AlphaScopeServer/
RUN dotnet restore AlphaScopeServer/AlphaScopeServer.csproj

# Copy the rest of the server sources and publish a Release build.
COPY AlphaScopeServer/. AlphaScopeServer/
RUN dotnet publish AlphaScopeServer/AlphaScopeServer.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# On small instances (Render free = 512 MB), workstation GC uses noticeably less memory
# than the default server GC and is plenty for our light traffic.
ENV DOTNET_gcServer=0

# Render injects $PORT at runtime and expects the server to bind to it (host 0.0.0.0).
# Dockerfile ENV cannot expand env vars, so we use shell-form entrypoint to let the
# shell substitute $PORT when the container starts. Fallback to 10000 for local docker runs.
EXPOSE 10000
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000} dotnet AlphaScopeServer.dll"]
