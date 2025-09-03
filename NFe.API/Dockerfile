# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files for better caching
COPY *.sln ./
COPY NFe.API/*.csproj ./NFe.API/
COPY NFe.Core/*.csproj ./NFe.Core/
COPY NFe.Infrastructure/*.csproj ./NFe.Infrastructure/
 
# Restore dependencies
RUN dotnet restore NFe.API/NFe.API.csproj

# Copy source code (exclude Worker to avoid conflicts)
COPY NFe.API/ ./NFe.API/
COPY NFe.Core/ ./NFe.Core/
COPY NFe.Infrastructure/ ./NFe.Infrastructure/

# Build and publish
RUN dotnet publish NFe.API/NFe.API.csproj -c Release -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r nfeapi && useradd -r -g nfeapi nfeapi

# Copy published files
COPY --from=build /app/publish .

# Set ownership and permissions
RUN chown -R nfeapi:nfeapi /app
USER nfeapi


# Performance optimizations
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
ENV DOTNET_USE_POLLING_FILE_WATCHER=false
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "NFe.API.dll"]
