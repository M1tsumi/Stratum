<p align="center">
  <img src="docs/stratum.png" alt="Stratum" width="400">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10-purple" alt=".NET 10">
  <img src="https://img.shields.io/badge/License-MIT-blue" alt="License">
  <img src="https://img.shields.io/badge/S3-Compatible-green" alt="S3 Compatible">
  <img src="https://img.shields.io/badge/Architecture-Clean%20Architecture-orange" alt="Clean Architecture">
  <img src="https://img.shields.io/badge/HTTP%2F3-Supported-red" alt="HTTP/3">
</p>

# Stratum

A high-performance, S3-compatible object storage server designed for local hosting and edge computing. Built with .NET 10, Clean Architecture principles, and optimized for production workloads.

## 🚀 Key Features

- **S3 API Compatible** - Full compatibility with AWS S3 API for seamless integration
- **High Performance** - Optimized file I/O with 1MB buffers, LRU caching, and concurrent operation throttling
- **Clean Architecture** - Built with Domain-Driven Design (DDD) and CQRS patterns
- **Production Ready** - Comprehensive metrics, health checks, configuration validation, and structured logging
- **Extensible** - Plugin system architecture for custom storage backends and authentication providers
- **Modern Stack** - HTTP/3 support, OpenTelemetry tracing, Serilog structured logging
- **Multipart Uploads** - Efficient handling of large file uploads with parallel part processing

## 📊 Performance Optimizations

Stratum includes several performance optimizations out of the box:

- **1MB File Buffers** - Optimized buffer size for maximum I/O throughput
- **LRU Cache** - In-memory caching with configurable size limits (default: 1000 items, 1GB)
- **Concurrent Throttling** - Semaphore-based throttling (default: 100 concurrent operations)
- **SQLite Indexing** - Comprehensive indexes on all tables for fast metadata queries
- **Memory-Mapped I/O** - 256MB mmap_size for improved database performance
- **WAL Mode** - Write-Ahead Logging for better SQLite write performance

## 🏗️ Architecture

Stratum follows Clean Architecture principles with clear separation of concerns:

```
Stratum/
├── src/
│   ├── Stratum.Domain/              # Core domain logic, entities, interfaces
│   ├── Stratum.Application/         # CQRS handlers, application services
│   ├── Stratum.Infrastructure/      # Storage implementations, authentication
│   │   ├── Storage/
│   │   │   ├── FileSystem/          # Filesystem storage with caching
│   │   │   └── Metadata/
│   │   │       └── SQLite/          # SQLite metadata store with indexing
│   │   └── Authentication/
│   │       └── SigV4/              # AWS Signature V4 authentication
│   ├── Stratum.Api/                 # REST API, middleware, endpoints
│   │   ├── Endpoints/               # Minimal API endpoints
│   │   ├── Middleware/              # Custom middleware (metrics, validation)
│   │   ├── HealthChecks/            # Health check implementations
│   │   └── Metrics/                 # Performance metrics collection
│   └── Stratum.PluginContracts/     # Plugin interfaces for extensibility
├── tests/
│   ├── Stratum.UnitTests/          # Unit tests
│   └── Stratum.IntegrationTests/    # Integration tests
└── deploy/
    └── docker/                     # Docker configuration
```

## 🚦 Quick Start

### Using .NET CLI

```bash
# Build the project
dotnet build

# Run the API server
dotnet run --project src/Stratum.Api/Stratum.Api.csproj
```

### Using Docker

```bash
docker-compose up -d
```

### Using AWS CLI

```bash
# Configure AWS profile for Stratum
aws configure --profile stratum

# Set endpoint URL
export AWS_ENDPOINT_URL=http://localhost:9000

# List buckets
aws s3 ls --endpoint-url http://localhost:9000 --profile stratum

# Create a bucket
aws s3 mb s3://my-bucket --endpoint-url http://localhost:9000 --profile stratum

# Upload a file
aws s3 cp file.txt s3://my-bucket/file.txt --endpoint-url http://localhost:9000 --profile stratum

# Download a file
aws s3 cp s3://my-bucket/file.txt downloaded.txt --endpoint-url http://localhost:9000 --profile stratum
```

## ⚙️ Configuration

Configuration can be managed via `appsettings.json` or environment variables:

```json
{
  "Server": {
    "ListenUrl": "http://0.0.0.0:9000",
    "EnableHttp3": true,
    "MaxRequestBodySize": 5368709120,
    "MaxRequestBufferSize": 1048576,
    "RequestTimeoutSeconds": 300
  },
  "Storage": {
    "DataDirectory": "./data",
    "MetadataBackend": "SQLite",
    "MaxConcurrentOperations": 100,
    "MaxCacheSize": 1000,
    "MaxCacheBytes": 1073741824
  },
  "Authentication": {
    "EnableAnonymousAccess": false
  },
  "Cors": {
    "AllowedOrigins": "*",
    "AllowedMethods": "*",
    "AllowedHeaders": "*"
  }
}
```

### Environment Variables

```bash
export Server__ListenUrl=http://0.0.0.0:9000
export Storage__DataDirectory=./data
export Storage__MaxConcurrentOperations=100
export Storage__MaxCacheSize=1000
```

See [docs/CONFIGURATION.md](docs/CONFIGURATION.md) for complete configuration options.

## 📡 API Endpoints

### Bucket Operations

| Method | Endpoint | Description |
|--------|----------|-------------|
| PUT | `/{bucket}` | Create a new bucket |
| DELETE | `/{bucket}` | Delete a bucket |
| HEAD | `/{bucket}` | Check if bucket exists |
| GET | `/` | List all buckets |

### Object Operations

| Method | Endpoint | Description |
|--------|----------|-------------|
| PUT | `/{bucket}/{*key}` | Upload an object |
| GET | `/{bucket}/{*key}` | Download an object |
| HEAD | `/{bucket}/{*key}` | Get object metadata |
| DELETE | `/{bucket}/{*key}` | Delete an object |
| POST | `/{bucket}/delete` | Delete multiple objects |
| GET | `/{bucket}` | List objects (V2) |

### Multipart Upload Operations

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/{bucket}/{*key}?uploads` | Initiate multipart upload |
| PUT | `/{bucket}/{*key}?partNumber={n}&uploadId={id}` | Upload a part |
| POST | `/{bucket}/{*key}?uploadId={id}` | Complete multipart upload |
| DELETE | `/{bucket}/{*key}?uploadId={id}` | Abort multipart upload |
| GET | `/{bucket}/uploads` | List multipart uploads |
| GET | `/{bucket}/parts` | List parts for an upload |

### Monitoring Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Health check with cache statistics |
| GET | `/metrics` | Performance metrics and uptime |
| GET | `/` | API information and status |

## 🔧 Tech Stack

- **.NET 10 / C# 13** - Latest .NET platform for maximum performance
- **Kestrel** - High-performance web server with HTTP/3 support
- **SQLite** - Embedded database for metadata storage with WAL mode
- **Filesystem** - Local filesystem for object storage
- **AWS Signature V4** - S3-compatible authentication
- **Serilog** - Structured logging with file and console outputs
- **OpenTelemetry** - Distributed tracing and metrics
- **System.IO.Pipelines** - High-performance streaming with backpressure

## 🧪 Development

### Running Tests

```bash
# Run all tests
dotnet test

# Run unit tests only
dotnet test tests/Stratum.UnitTests

# Run integration tests only
dotnet test tests/Stratum.IntegrationTests
```

### Code Quality

- Follow Clean Architecture principles
- Use conventional commits: `feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`
- Maintain test coverage above 80%
- Use async/await for all I/O operations

## 🗺️ Roadmap

### Near Term
- [ ] RocksDB metadata backend for better performance
- [ ] S3 gateway storage backend for cloud tiering
- [ ] Event-driven architecture with message bus
- [ ] Plugin system implementation

### Mid Term
- [ ] Blazor management UI
- [ ] Server-side encryption (SSE-C, SSE-S3)
- [ ] Object versioning and lifecycle policies
- [ ] Kubernetes Helm charts

### Long Term
- [ ] Distributed storage with erasure coding
- [ ] Multi-region replication
- [ ] Advanced analytics and monitoring dashboard
- [ ] GraphQL API support

## 📈 Monitoring & Observability

Stratum provides comprehensive monitoring capabilities:

- **Health Checks** - `/health` endpoint for system health and cache statistics
- **Performance Metrics** - `/metrics` endpoint for operation latency, error rates, and throughput
- **Structured Logging** - Serilog with file rotation and structured output
- **Request Tracing** - Request ID tracking for distributed tracing
- **Error Context** - Detailed error messages with timestamps and error types

## 🔒 Security

- AWS Signature V4 authentication
- Configurable anonymous access
- Request validation and size limits
- CORS support
- Rate limiting (configurable)

## 📄 License

MIT License - see [LICENSE](LICENSE) file for details

## 🤝 Contributing

Contributions are welcome! Please read our contributing guidelines before submitting PRs.

## 📞 Support

- GitHub Issues: [github.com/M1tsumi/Stratum/issues](https://github.com/M1tsumi/Stratum/issues)
- Documentation: [docs/](docs/)

