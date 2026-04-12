# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and projects
COPY ["src/Stratum.Domain/Stratum.Domain.csproj", "src/Stratum.Domain/"]
COPY ["src/Stratum.Application/Stratum.Application.csproj", "src/Stratum.Application/"]
COPY ["src/Stratum.Infrastructure/Stratum.Infrastructure.csproj", "src/Stratum.Infrastructure/"]
COPY ["src/Stratum.Api/Stratum.Api.csproj", "src/Stratum.Api/"]
COPY ["src/Stratum.PluginContracts/Stratum.PluginContracts.csproj", "src/Stratum.PluginContracts/"]

# Restore dependencies
RUN dotnet restore "src/Stratum.Api/Stratum.Api.csproj"

# Copy all source files
COPY src/ .

# Build the project
WORKDIR "/src/src/Stratum.Api"
RUN dotnet build "Stratum.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Stratum.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install required system packages
RUN apt-get update && apt-get install -y \
    libsqlite3-dev \
    && rm -rf /var/lib/apt/lists/*

# Create data directory
RUN mkdir -p /data

# Copy published application
COPY --from=publish /app/publish .

# Expose port
EXPOSE 9000

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:9000/health || exit 1

ENTRYPOINT ["dotnet", "Stratum.Api.dll"]
