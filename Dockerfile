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

# Render injects $PORT; default is 10000. Listen on all interfaces so the proxy can reach us.
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

# On small instances (Render free = 512 MB), workstation GC uses noticeably less memory
# than the default server GC and is plenty for our light traffic.
ENV DOTNET_gcServer=0

ENTRYPOINT ["dotnet", "AlphaScopeServer.dll"]
