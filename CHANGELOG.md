# Changelog

All notable changes to Stratum will be documented in this file.

## [0.1.0-alpha.1] - 2026-04-12

### Added
- Clean Architecture with Domain, Application, Infrastructure, and API layers
- Domain entities: Bucket, ObjectMetadata, MultipartUpload, AccessKey
- Value objects: ETag, ObjectKey, BucketName
- Domain services: ETagCalculator, SignatureCalculator, ObjectKeyValidator
- CQRS infrastructure with MediatR
- SQLite metadata store with WAL mode
- FileSystem object store with streaming support
- AWS Signature Version 4 validator
- URL normalization middleware for S3-style URLs
- Bucket API endpoints (Create, Delete, List, Head)
- Object API endpoints (Put, Get, Head, Delete, ListObjectsV2)
- Multipart upload endpoints (Initiate, Upload Part, Complete, Abort, List)
- OpenTelemetry integration for tracing and metrics
- Serilog logging with console and file sinks
- Docker support with Dockerfile and docker-compose.yml
- Unit tests with xUnit
- Comprehensive documentation

### Technology
- .NET 10 / C# 13
- Kestrel with HTTP/3 support
- SQLite for metadata
- Native filesystem for object storage
- Serilog for logging
- OpenTelemetry for observability
