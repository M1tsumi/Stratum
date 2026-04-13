namespace Stratum.Infrastructure.Metadata.SQLite;

using Microsoft.Data.Sqlite;
using Stratum.Domain.Entities;
using Stratum.Domain.Interfaces;
using System.Text.Json;

/// <summary>
/// SQLite implementation of the metadata store.
/// Uses WAL mode for performance and connection pooling for efficiency.
/// </summary>
public sealed class SQLiteMetadataStore : IMetadataStore, IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the SQLiteMetadataStore class.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public SQLiteMetadataStore(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Initializes the database schema.
    /// </summary>
    private async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_connection != null)
        {
            return;
        }

        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync(cancellationToken);

        // Enable WAL mode for better performance
        await using var walCommand = _connection.CreateCommand();
        walCommand.CommandText = "PRAGMA journal_mode = WAL;";
        await walCommand.ExecuteNonQueryAsync(cancellationToken);

        // Set cache size (64MB)
        await using var cacheCommand = _connection.CreateCommand();
        cacheCommand.CommandText = "PRAGMA cache_size = -64000;";
        await cacheCommand.ExecuteNonQueryAsync(cancellationToken);

        // Optimize for performance
        await using var optimizeCommand = _connection.CreateCommand();
        optimizeCommand.CommandText = "PRAGMA synchronous = NORMAL;";
        await optimizeCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var mmapCommand = _connection.CreateCommand();
        mmapCommand.CommandText = "PRAGMA mmap_size = 268435456;"; // 256MB
        await mmapCommand.ExecuteNonQueryAsync(cancellationToken);

        // Create tables
        await CreateTablesAsync(cancellationToken);
    }

    /// <summary>
    /// Creates the database tables.
    /// </summary>
    private async Task CreateTablesAsync(CancellationToken cancellationToken)
    {
        var createTablesSql = @"
            CREATE TABLE IF NOT EXISTS Buckets (
                Name TEXT PRIMARY KEY,
                CreatedAt TEXT NOT NULL,
                Region TEXT NOT NULL,
                OwnerId TEXT,
                OwnerDisplayName TEXT,
                ObjectCount INTEGER DEFAULT 0,
                TotalSize INTEGER DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS Objects (
                BucketName TEXT NOT NULL,
                Key TEXT NOT NULL,
                ETag TEXT NOT NULL,
                Size INTEGER NOT NULL,
                ContentType TEXT NOT NULL,
                ContentEncoding TEXT,
                ContentDisposition TEXT,
                ContentLanguage TEXT,
                CacheControl TEXT,
                UserMetadata TEXT,
                StorageClass TEXT DEFAULT 'STANDARD',
                VersionId TEXT,
                IsLatest INTEGER DEFAULT 1,
                IsDeleteMarker INTEGER DEFAULT 0,
                LastModified TEXT NOT NULL,
                PRIMARY KEY (BucketName, Key)
            );

            CREATE INDEX IF NOT EXISTS IX_Objects_BucketKey ON Objects(BucketName, Key);
            CREATE INDEX IF NOT EXISTS IX_Objects_BucketName ON Objects(BucketName);
            CREATE INDEX IF NOT EXISTS IX_Objects_LastModified ON Objects(LastModified);
            CREATE INDEX IF NOT EXISTS IX_Objects_ETag ON Objects(ETag);
            CREATE INDEX IF NOT EXISTS IX_Objects_VersionId ON Objects(VersionId);

            CREATE TABLE IF NOT EXISTS MultipartUploads (
                UploadId TEXT PRIMARY KEY,
                BucketName TEXT NOT NULL,
                Key TEXT NOT NULL,
                InitiatedAt TEXT NOT NULL,
                OwnerId TEXT,
                OwnerDisplayName TEXT,
                ContentType TEXT,
                UserMetadata TEXT,
                StorageClass TEXT DEFAULT 'STANDARD'
            );

            CREATE INDEX IF NOT EXISTS IX_MultipartUploads_BucketName ON MultipartUploads(BucketName);
            CREATE INDEX IF NOT EXISTS IX_MultipartUploads_InitiatedAt ON MultipartUploads(InitiatedAt);

            CREATE TABLE IF NOT EXISTS MultipartParts (
                UploadId TEXT NOT NULL,
                PartNumber INTEGER NOT NULL,
                ETag TEXT NOT NULL,
                Size INTEGER NOT NULL,
                LastModified TEXT NOT NULL,
                PRIMARY KEY (UploadId, PartNumber)
            );

            CREATE INDEX IF NOT EXISTS IX_MultipartParts_UploadId ON MultipartParts(UploadId);

            CREATE TABLE IF NOT EXISTS AccessKeys (
                AccessKeyId TEXT PRIMARY KEY,
                SecretAccessKeyHash TEXT NOT NULL,
                UserName TEXT,
                CreatedAt TEXT NOT NULL,
                ExpiresAt TEXT,
                IsActive INTEGER DEFAULT 1,
                Description TEXT
            );

            CREATE INDEX IF NOT EXISTS IX_AccessKeys_UserName ON AccessKeys(UserName);
            CREATE INDEX IF NOT EXISTS IX_AccessKeys_IsActive ON AccessKeys(IsActive);
            CREATE INDEX IF NOT EXISTS IX_AccessKeys_ExpiresAt ON AccessKeys(ExpiresAt);

            CREATE TABLE IF NOT EXISTS AccessKeyPolicies (
                AccessKeyId TEXT NOT NULL,
                Policy TEXT NOT NULL,
                PRIMARY KEY (AccessKeyId, Policy),
                FOREIGN KEY (AccessKeyId) REFERENCES AccessKeys(AccessKeyId) ON DELETE CASCADE
            );
        ";

        await using var command = _connection.CreateCommand();
        command.CommandText = createTablesSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Bucket operations

    public async Task CreateBucketAsync(Bucket bucket, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Buckets (Name, CreatedAt, Region, OwnerId, OwnerDisplayName, ObjectCount, TotalSize)
            VALUES (@Name, @CreatedAt, @Region, @OwnerId, @OwnerDisplayName, @ObjectCount, @TotalSize)";
        
        command.Parameters.AddWithValue("@Name", bucket.Name);
        command.Parameters.AddWithValue("@CreatedAt", bucket.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("@Region", bucket.Region);
        command.Parameters.AddWithValue("@OwnerId", bucket.OwnerId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@OwnerDisplayName", bucket.OwnerDisplayName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ObjectCount", bucket.ObjectCount);
        command.Parameters.AddWithValue("@TotalSize", bucket.TotalSize);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteBucketAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM Buckets WHERE Name = @Name";
        command.Parameters.AddWithValue("@Name", bucketName);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Bucket?> GetBucketAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT Name, CreatedAt, Region, OwnerId, OwnerDisplayName, ObjectCount, TotalSize
            FROM Buckets WHERE Name = @Name";
        command.Parameters.AddWithValue("@Name", bucketName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new Bucket(
                reader.GetString(0),
                DateTime.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt64(5),
                reader.GetInt64(6));
        }

        return null;
    }

    public async Task<IReadOnlyList<Bucket>> ListBucketsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT Name, CreatedAt, Region, OwnerId, OwnerDisplayName, ObjectCount, TotalSize FROM Buckets";

        var buckets = new List<Bucket>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            buckets.Add(new Bucket(
                reader.GetString(0),
                DateTime.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt64(5),
                reader.GetInt64(6)));
        }

        return buckets.AsReadOnly();
    }

    public async Task<bool> BucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Buckets WHERE Name = @Name";
        command.Parameters.AddWithValue("@Name", bucketName);

        var result = (long)await command.ExecuteScalarAsync(cancellationToken)!;
        return result > 0;
    }

    public async Task UpdateBucketStatisticsAsync(string bucketName, long objectCountDelta, long sizeDelta, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var command = _connection.CreateCommand();
        command.CommandText = @"
            UPDATE Buckets 
            SET ObjectCount = ObjectCount + @ObjectCountDelta,
                TotalSize = TotalSize + @SizeDelta
            WHERE Name = @Name";
        command.Parameters.AddWithValue("@Name", bucketName);
        command.Parameters.AddWithValue("@ObjectCountDelta", objectCountDelta);
        command.Parameters.AddWithValue("@SizeDelta", sizeDelta);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Object operations

    public async Task PutObjectMetadataAsync(ObjectMetadata metadata, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        var userMetadataJson = JsonSerializer.Serialize(metadata.UserMetadata);

        await using var command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO Objects 
            (BucketName, Key, ETag, Size, ContentType, ContentEncoding, ContentDisposition, 
             ContentLanguage, CacheControl, UserMetadata, StorageClass, VersionId, IsLatest, 
             IsDeleteMarker, LastModified)
            VALUES (@BucketName, @Key, @ETag, @Size, @ContentType, @ContentEncoding, @ContentDisposition,
                    @ContentLanguage, @CacheControl, @UserMetadata, @StorageClass, @VersionId, @IsLatest,
                    @IsDeleteMarker, @LastModified)";

        command.Parameters.AddWithValue("@BucketName", metadata.BucketName);
        command.Parameters.AddWithValue("@Key", metadata.Key);
        command.Parameters.AddWithValue("@ETag", metadata.ETag);
        command.Parameters.AddWithValue("@Size", metadata.Size);
        command.Parameters.AddWithValue("@ContentType", metadata.ContentType);
        command.Parameters.AddWithValue("@ContentEncoding", metadata.ContentEncoding ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ContentDisposition", metadata.ContentDisposition ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ContentLanguage", metadata.ContentLanguage ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@CacheControl", metadata.CacheControl ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@UserMetadata", userMetadataJson);
        command.Parameters.AddWithValue("@StorageClass", metadata.StorageClass);
        command.Parameters.AddWithValue("@VersionId", metadata.VersionId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@IsLatest", metadata.IsLatest ? 1 : 0);
        command.Parameters.AddWithValue("@IsDeleteMarker", metadata.IsDeleteMarker ? 1 : 0);
        command.Parameters.AddWithValue("@LastModified", metadata.LastModified.ToString("o"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ObjectMetadata?> GetObjectMetadataAsync(string bucketName, string key, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT BucketName, Key, ETag, Size, ContentType, ContentEncoding, ContentDisposition,
                   ContentLanguage, CacheControl, UserMetadata, StorageClass, VersionId, IsLatest,
                   IsDeleteMarker, LastModified
            FROM Objects WHERE BucketName = @BucketName AND Key = @Key";
        command.Parameters.AddWithValue("@BucketName", bucketName);
        command.Parameters.AddWithValue("@Key", key);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var userMetadataJson = reader.IsDBNull(9) ? null : reader.GetString(9);
            var userMetadata = string.IsNullOrEmpty(userMetadataJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(userMetadataJson) ?? new Dictionary<string, string>();

            return new ObjectMetadata(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                userMetadata,
                reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.GetBoolean(12),
                reader.GetBoolean(13),
                DateTime.Parse(reader.GetString(14)));
        }

        return null;
    }

    public async Task DeleteObjectMetadataAsync(string bucketName, string key, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM Objects WHERE BucketName = @BucketName AND Key = @Key";
        command.Parameters.AddWithValue("@BucketName", bucketName);
        command.Parameters.AddWithValue("@Key", key);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteMultipleObjectMetadataAsync(string bucketName, IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        var keyList = keys.ToList();
        if (keyList.Count == 0)
        {
            return;
        }

        var placeholders = string.Join(",", keyList.Select((_, i) => $"@Key{i}"));
        await using var command = _connection.CreateCommand();
        command.CommandText = $"DELETE FROM Objects WHERE BucketName = @BucketName AND Key IN ({placeholders})";
        command.Parameters.AddWithValue("@BucketName", bucketName);

        for (var i = 0; i < keyList.Count; i++)
        {
            command.Parameters.AddWithValue($"@Key{i}", keyList[i]);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> ObjectExistsAsync(string bucketName, string key, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Objects WHERE BucketName = @BucketName AND Key = @Key";
        command.Parameters.AddWithValue("@BucketName", bucketName);
        command.Parameters.AddWithValue("@Key", key);

        var result = (long)await command.ExecuteScalarAsync(cancellationToken)!;
        return result > 0;
    }

    public async IAsyncEnumerable<ObjectMetadata> ListObjectsAsync(
        string bucketName,
        string? prefix = null,
        string? delimiter = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        var sql = "SELECT BucketName, Key, ETag, Size, ContentType, ContentEncoding, ContentDisposition, ContentLanguage, CacheControl, UserMetadata, StorageClass, VersionId, IsLatest, IsDeleteMarker, LastModified FROM Objects WHERE BucketName = @BucketName";

        if (!string.IsNullOrEmpty(prefix))
        {
            sql += " AND Key LIKE @Prefix || '%'";
        }

        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@BucketName", bucketName);

        if (!string.IsNullOrEmpty(prefix))
        {
            command.Parameters.AddWithValue("@Prefix", prefix);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var userMetadataJson = reader.IsDBNull(10) ? null : reader.GetString(10);
            var userMetadata = string.IsNullOrEmpty(userMetadataJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(userMetadataJson) ?? new Dictionary<string, string>();

            yield return new ObjectMetadata(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                userMetadata,
                reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.GetBoolean(11),
                reader.GetBoolean(12),
                DateTime.Parse(reader.GetString(13)));
        }
    }

    public async Task<(IReadOnlyList<ObjectMetadata> Objects, IReadOnlyList<string> CommonPrefixes, string? NextContinuationToken)> ListObjectsV2Async(
        string bucketName,
        string? prefix = null,
        string? delimiter = null,
        string? continuationToken = null,
        int maxKeys = 1000,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        var objects = new List<ObjectMetadata>();
        var commonPrefixes = new HashSet<string>();
        string? nextContinuationToken = null;

        var sql = @"
            SELECT BucketName, Key, ETag, Size, ContentType, ContentEncoding, ContentDisposition,
                   ContentLanguage, CacheControl, UserMetadata, StorageClass, VersionId, IsLatest,
                   IsDeleteMarker, LastModified
            FROM Objects 
            WHERE BucketName = @BucketName";

        if (!string.IsNullOrEmpty(prefix))
        {
            sql += " AND Key LIKE @Prefix || '%'";
        }

        if (!string.IsNullOrEmpty(continuationToken))
        {
            sql += " AND Key > @ContinuationToken";
        }

        sql += " ORDER BY Key LIMIT @MaxKeys";

        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@BucketName", bucketName);
        command.Parameters.AddWithValue("@MaxKeys", maxKeys);

        if (!string.IsNullOrEmpty(prefix))
        {
            command.Parameters.AddWithValue("@Prefix", prefix);
        }

        if (!string.IsNullOrEmpty(continuationToken))
        {
            command.Parameters.AddWithValue("@ContinuationToken", continuationToken);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var key = reader.GetString(1);

            // Handle delimiter for common prefixes
            if (!string.IsNullOrEmpty(delimiter))
            {
                var prefixIndex = key.IndexOf(delimiter, StringComparison.Ordinal);
                if (prefixIndex >= 0)
                {
                    var commonPrefix = key[..(prefixIndex + delimiter.Length)];
                    commonPrefixes.Add(commonPrefix);
                    continue;
                }
            }

            var userMetadataJson = reader.IsDBNull(10) ? null : reader.GetString(10);
            var userMetadata = string.IsNullOrEmpty(userMetadataJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(userMetadataJson) ?? new Dictionary<string, string>();

            objects.Add(new ObjectMetadata(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                userMetadata,
                reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.GetBoolean(11),
                reader.GetBoolean(12),
                DateTime.Parse(reader.GetString(13))));

            nextContinuationToken = key;
        }

        return (objects.AsReadOnly(), commonPrefixes.ToList().AsReadOnly(), nextContinuationToken);
    }

    // Multipart upload operations

    public async Task CreateMultipartUploadAsync(MultipartUpload upload, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        var userMetadataJson = JsonSerializer.Serialize(upload.UserMetadata);

        await using var command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO MultipartUploads (UploadId, BucketName, Key, InitiatedAt, OwnerId, OwnerDisplayName, ContentType, UserMetadata, StorageClass)
            VALUES (@UploadId, @BucketName, @Key, @InitiatedAt, @OwnerId, @OwnerDisplayName, @ContentType, @UserMetadata, @StorageClass)";

        command.Parameters.AddWithValue("@UploadId", upload.UploadId);
        command.Parameters.AddWithValue("@BucketName", upload.BucketName);
        command.Parameters.AddWithValue("@Key", upload.Key);
        command.Parameters.AddWithValue("@InitiatedAt", upload.InitiatedAt.ToString("o"));
        command.Parameters.AddWithValue("@OwnerId", upload.OwnerId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@OwnerDisplayName", upload.OwnerDisplayName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@ContentType", upload.ContentType ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@UserMetadata", userMetadataJson);
        command.Parameters.AddWithValue("@StorageClass", upload.StorageClass);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<MultipartUpload?> GetMultipartUploadAsync(string uploadId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT UploadId, BucketName, Key, InitiatedAt, OwnerId, OwnerDisplayName, ContentType, UserMetadata, StorageClass
            FROM MultipartUploads WHERE UploadId = @UploadId";
        command.Parameters.AddWithValue("@UploadId", uploadId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var userMetadataJson = reader.IsDBNull(7) ? null : reader.GetString(7);
            var userMetadata = string.IsNullOrEmpty(userMetadataJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(userMetadataJson) ?? new Dictionary<string, string>();

            // Load parts
            var parts = await GetPartsAsync(uploadId, cancellationToken);

            return new MultipartUpload(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTime.Parse(reader.GetString(3)),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                userMetadata,
                reader.GetString(8),
                parts);
        }

        return null;
    }

    public async Task<IReadOnlyList<MultipartUpload>> ListMultipartUploadsAsync(string bucketName, string? prefix = null, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        var sql = "SELECT UploadId, BucketName, Key, InitiatedAt, OwnerId, OwnerDisplayName, ContentType, UserMetadata, StorageClass FROM MultipartUploads WHERE BucketName = @BucketName";

        if (!string.IsNullOrEmpty(prefix))
        {
            sql += " AND Key LIKE @Prefix || '%'";
        }

        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@BucketName", bucketName);

        if (!string.IsNullOrEmpty(prefix))
        {
            command.Parameters.AddWithValue("@Prefix", prefix);
        }

        var uploads = new List<MultipartUpload>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var userMetadataJson = reader.IsDBNull(7) ? null : reader.GetString(7);
            var userMetadata = string.IsNullOrEmpty(userMetadataJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(userMetadataJson) ?? new Dictionary<string, string>();

            uploads.Add(new MultipartUpload(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTime.Parse(reader.GetString(3)),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                userMetadata,
                reader.GetString(8),
                new List<MultipartPart>()));
        }

        return uploads.AsReadOnly();
    }

    public async Task AddPartAsync(string uploadId, MultipartPart part, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO MultipartParts (UploadId, PartNumber, ETag, Size, LastModified)
            VALUES (@UploadId, @PartNumber, @ETag, @Size, @LastModified)";

        command.Parameters.AddWithValue("@UploadId", uploadId);
        command.Parameters.AddWithValue("@PartNumber", part.PartNumber);
        command.Parameters.AddWithValue("@ETag", part.ETag);
        command.Parameters.AddWithValue("@Size", part.Size);
        command.Parameters.AddWithValue("@LastModified", part.LastModified.ToString("o"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CompleteMultipartUploadAsync(string uploadId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);
        try
        {
            // Delete parts
            await using var deletePartsCommand = _connection.CreateCommand();
            deletePartsCommand.CommandText = "DELETE FROM MultipartParts WHERE UploadId = @UploadId";
            deletePartsCommand.Parameters.AddWithValue("@UploadId", uploadId);
            await deletePartsCommand.ExecuteNonQueryAsync(cancellationToken);

            // Delete upload record
            await using var deleteUploadCommand = _connection.CreateCommand();
            deleteUploadCommand.CommandText = "DELETE FROM MultipartUploads WHERE UploadId = @UploadId";
            deleteUploadCommand.Parameters.AddWithValue("@UploadId", uploadId);
            await deleteUploadCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task AbortMultipartUploadAsync(string uploadId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);
        try
        {
            // Delete parts
            await using var deletePartsCommand = _connection.CreateCommand();
            deletePartsCommand.CommandText = "DELETE FROM MultipartParts WHERE UploadId = @UploadId";
            deletePartsCommand.Parameters.AddWithValue("@UploadId", uploadId);
            await deletePartsCommand.ExecuteNonQueryAsync(cancellationToken);

            // Delete upload record
            await using var deleteUploadCommand = _connection.CreateCommand();
            deleteUploadCommand.CommandText = "DELETE FROM MultipartUploads WHERE UploadId = @UploadId";
            deleteUploadCommand.Parameters.AddWithValue("@UploadId", uploadId);
            await deleteUploadCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // Access key operations

    public async Task CreateAccessKeyAsync(AccessKey accessKey, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);
        try
        {
            // Insert access key
            await using var command = _connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO AccessKeys (AccessKeyId, SecretAccessKeyHash, UserName, CreatedAt, ExpiresAt, IsActive, Description)
                VALUES (@AccessKeyId, @SecretAccessKeyHash, @UserName, @CreatedAt, @ExpiresAt, @IsActive, @Description)";

            command.Parameters.AddWithValue("@AccessKeyId", accessKey.AccessKeyId);
            command.Parameters.AddWithValue("@SecretAccessKeyHash", accessKey.SecretAccessKeyHash);
            command.Parameters.AddWithValue("@UserName", accessKey.UserName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@CreatedAt", accessKey.CreatedAt.ToString("o"));
            command.Parameters.AddWithValue("@ExpiresAt", accessKey.ExpiresAt.HasValue ? accessKey.ExpiresAt.Value.ToString("o") : (object)DBNull.Value);
            command.Parameters.AddWithValue("@IsActive", accessKey.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("@Description", accessKey.Description ?? (object)DBNull.Value);

            await command.ExecuteNonQueryAsync(cancellationToken);

            // Insert policies
            foreach (var policy in accessKey.Policies)
            {
                await using var policyCommand = _connection.CreateCommand();
                policyCommand.CommandText = "INSERT INTO AccessKeyPolicies (AccessKeyId, Policy) VALUES (@AccessKeyId, @Policy)";
                policyCommand.Parameters.AddWithValue("@AccessKeyId", accessKey.AccessKeyId);
                policyCommand.Parameters.AddWithValue("@Policy", policy);
                await policyCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<AccessKey?> GetAccessKeyAsync(string accessKeyId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT AccessKeyId, SecretAccessKeyHash, UserName, CreatedAt, ExpiresAt, IsActive, Description
            FROM AccessKeys WHERE AccessKeyId = @AccessKeyId";
        command.Parameters.AddWithValue("@AccessKeyId", accessKeyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var policies = await GetPoliciesAsync(accessKeyId, cancellationToken);

            return new AccessKey(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                DateTime.Parse(reader.GetString(3)),
                reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
                reader.GetBoolean(5),
                policies,
                reader.IsDBNull(6) ? null : reader.GetString(6));
        }

        return null;
    }

    public async Task<IReadOnlyList<AccessKey>> ListAccessKeysAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT AccessKeyId, SecretAccessKeyHash, UserName, CreatedAt, ExpiresAt, IsActive, Description FROM AccessKeys";

        var keys = new List<AccessKey>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var accessKeyId = reader.GetString(0);
            var policies = await GetPoliciesAsync(accessKeyId, cancellationToken);

            keys.Add(new AccessKey(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                DateTime.Parse(reader.GetString(3)),
                reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
                reader.GetBoolean(5),
                policies,
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return keys.AsReadOnly();
    }

    public async Task DeleteAccessKeyAsync(string accessKeyId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM AccessKeys WHERE AccessKeyId = @AccessKeyId";
        command.Parameters.AddWithValue("@AccessKeyId", accessKeyId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAccessKeyAsync(AccessKey accessKey, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var transaction = await _connection.BeginTransactionAsync(cancellationToken);
        try
        {
            // Update access key
            await using var command = _connection.CreateCommand();
            command.CommandText = @"
                UPDATE AccessKeys 
                SET SecretAccessKeyHash = @SecretAccessKeyHash, UserName = @UserName, ExpiresAt = @ExpiresAt, IsActive = @IsActive, Description = @Description
                WHERE AccessKeyId = @AccessKeyId";

            command.Parameters.AddWithValue("@AccessKeyId", accessKey.AccessKeyId);
            command.Parameters.AddWithValue("@SecretAccessKeyHash", accessKey.SecretAccessKeyHash);
            command.Parameters.AddWithValue("@UserName", accessKey.UserName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ExpiresAt", accessKey.ExpiresAt.HasValue ? accessKey.ExpiresAt.Value.ToString("o") : (object)DBNull.Value);
            command.Parameters.AddWithValue("@IsActive", accessKey.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("@Description", accessKey.Description ?? (object)DBNull.Value);

            await command.ExecuteNonQueryAsync(cancellationToken);

            // Delete existing policies
            await using var deletePoliciesCommand = _connection.CreateCommand();
            deletePoliciesCommand.CommandText = "DELETE FROM AccessKeyPolicies WHERE AccessKeyId = @AccessKeyId";
            deletePoliciesCommand.Parameters.AddWithValue("@AccessKeyId", accessKey.AccessKeyId);
            await deletePoliciesCommand.ExecuteNonQueryAsync(cancellationToken);

            // Insert new policies
            foreach (var policy in accessKey.Policies)
            {
                await using var policyCommand = _connection.CreateCommand();
                policyCommand.CommandText = "INSERT INTO AccessKeyPolicies (AccessKeyId, Policy) VALUES (@AccessKeyId, @Policy)";
                policyCommand.Parameters.AddWithValue("@AccessKeyId", accessKey.AccessKeyId);
                policyCommand.Parameters.AddWithValue("@Policy", policy);
                await policyCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // Helper methods

    private async Task<List<MultipartPart>> GetPartsAsync(string uploadId, CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT PartNumber, ETag, Size, LastModified FROM MultipartParts WHERE UploadId = @UploadId ORDER BY PartNumber";
        command.Parameters.AddWithValue("@UploadId", uploadId);

        var parts = new List<MultipartPart>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            parts.Add(new MultipartPart(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetInt64(2),
                DateTime.Parse(reader.GetString(3))));
        }

        return parts;
    }

    private async Task<List<string>> GetPoliciesAsync(string accessKeyId, CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT Policy FROM AccessKeyPolicies WHERE AccessKeyId = @AccessKeyId";
        command.Parameters.AddWithValue("@AccessKeyId", accessKeyId);

        var policies = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            policies.Add(reader.GetString(0));
        }

        return policies;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _connection?.Dispose();
        _disposed = true;
    }
}
