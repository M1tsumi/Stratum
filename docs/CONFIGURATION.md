# Configuration

Stratum uses ASP.NET Core configuration with support for JSON files, environment variables, and command-line arguments.

## Configuration Sources

Configuration is loaded in the following order (later sources override earlier ones):

1. `appsettings.json` - Base configuration
2. `appsettings.{Environment}.json` - Environment-specific configuration
3. Environment variables
4. Command-line arguments

## Environment Variables

All configuration values can be overridden using environment variables. Use double underscores (`__`) to separate sections:

```bash
# Server configuration
export Server__ListenUrl="http://0.0.0.0:9000"
export Server__EnableHttp3="true"

# Storage configuration
export Storage__DataDirectory="/var/lib/stratum"
export Storage__MaxObjectSize="10737418240"

# Authentication
export Authentication__EnableAnonymousAccess="false"
```

## Configuration Options

### Server

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Server:ListenUrl` | string | `http://0.0.0.0:9000` | The URL and port to listen on |
| `Server:EnableHttp3` | bool | `true` | Enable HTTP/3 support |
| `Server:MaxRequestBodySize` | long | `5368709120` (5GB) | Maximum request body size in bytes |
| `Server:MaxRequestBufferSize` | int | `1048576` (1MB) | Maximum request buffer size in bytes |
| `Server:RequestTimeoutSeconds` | int | `300` | Request timeout in seconds (default: 5 minutes) |

### Storage

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Storage:DataDirectory` | string | `./data` | Directory for storing data and metadata |
| `Storage:MetadataBackend` | string | `SQLite` | Metadata storage backend (currently only SQLite) |
| `Storage:MaxObjectSize` | long | `5368709120` (5GB) | Maximum object size in bytes |

### Authentication

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Authentication:EnableAnonymousAccess` | bool | `false` | Allow anonymous access (bypass authentication) |

### CORS

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Cors:AllowedOrigins` | string | `*` | Comma-separated list of allowed origins (use `*` for all) |
| `Cors:AllowedMethods` | string | `*` | Comma-separated list of allowed HTTP methods (use `*` for all) |
| `Cors:AllowedHeaders` | string | `*` | Comma-separated list of allowed headers (use `*` for all) |

### Response Compression

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ResponseCompression:EnableForHttps` | bool | `true` | Enable compression for HTTPS responses |
| `ResponseCompression:Providers` | array | `["Brotli", "Gzip"]` | Compression providers to use |

### Rate Limiting

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `RateLimit:MaxRequestsPerMinute` | int | `100` | Maximum requests per minute per client (0 to disable) |

### Logging

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Serilog:MinimumLevel:Default` | string | `Information` | Default log level |
| `Serilog:WriteTo` | array | - | Log output sinks (Console, File) |

## Docker Environment Variables

When using Docker, you can pass configuration via environment variables:

```yaml
services:
  stratum:
    image: stratum:latest
    environment:
      - Server__ListenUrl=http://0.0.0.0:9000
      - Storage__DataDirectory=/data
      - Authentication__EnableAnonymousAccess=true
    volumes:
      - ./data:/data
```

## Getting Started

1. Copy `appsettings.example.json` to `appsettings.json`
2. Modify configuration values as needed
3. Start the server

```bash
cp appsettings.example.json appsettings.json
dotnet run --project src/Stratum.Api/Stratum.Api.csproj
```
