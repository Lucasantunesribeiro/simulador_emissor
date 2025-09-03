# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files for better caching
COPY *.sln ./
COPY NFe.Core/*.csproj ./NFe.Core/
COPY NFe.Infrastructure/*.csproj ./NFe.Infrastructure/
COPY NFe.Worker/*.csproj ./NFe.Worker/

# Restore dependencies
RUN dotnet restore NFe.Worker/NFe.Worker.csproj

# Copy only required source code for Worker and dependencies
COPY NFe.Core/ ./NFe.Core/
COPY NFe.Infrastructure/ ./NFe.Infrastructure/
COPY NFe.Worker/ ./NFe.Worker/

# Build and publish
RUN dotnet publish NFe.Worker/NFe.Worker.csproj -c Release -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r nfeworker && useradd -r -g nfeworker nfeworker

# Copy published files
COPY --from=build /app/publish .

# Set ownership and permissions
RUN chown -R nfeworker:nfeworker /app
USER nfeworker

# Performance optimizations
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
ENV DOTNET_USE_POLLING_FILE_WATCHER=false
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "NFe.Worker.dll"]
