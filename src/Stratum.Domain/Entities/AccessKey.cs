namespace Stratum.Domain.Entities;

/// <summary>
/// Represents an access key used for authentication to the S3-compatible storage.
/// Access keys consist of an access key ID and a secret access key.
/// </summary>
public class AccessKey
{
    /// <summary>
    /// Gets the access key identifier.
    /// This is a 20-character alphanumeric string used to identify the key.
    /// </summary>
    public string AccessKeyId { get; }

    /// <summary>
    /// Gets the hashed secret access key.
    /// The secret key is stored as a hash for security, not in plaintext.
    /// </summary>
    public string SecretAccessKeyHash { get; }

    /// <summary>
    /// Gets the optional user name associated with this access key.
    /// </summary>
    public string? UserName { get; }

    /// <summary>
    /// Gets the date and time when this access key was created.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Gets the optional expiration date and time for this access key.
    /// If null, the key does not expire.
    /// </summary>
    public DateTime? ExpiresAt { get; }

    /// <summary>
    /// Gets whether this access key is currently active.
    /// Inactive keys cannot be used for authentication.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the list of policy references associated with this access key.
    /// These policies define the permissions granted to this key.
    /// </summary>
    public List<string> Policies { get; }

    /// <summary>
    /// Gets the optional description of this access key.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Initializes a new instance of the AccessKey class.
    /// </summary>
    /// <param name="accessKeyId">The access key identifier.</param>
    /// <param name="secretAccessKeyHash">The hashed secret access key.</param>
    /// <param name="userName">Optional user name.</param>
    /// <param name="expiresAt">Optional expiration date.</param>
    /// <param name="description">Optional description.</param>
    public AccessKey(
        string accessKeyId,
        string secretAccessKeyHash,
        string? userName = null,
        DateTime? expiresAt = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(accessKeyId))
        {
            throw new ArgumentException("Access key ID cannot be null or whitespace.", nameof(accessKeyId));
        }

        if (string.IsNullOrWhiteSpace(secretAccessKeyHash))
        {
            throw new ArgumentException("Secret access key hash cannot be null or whitespace.", nameof(secretAccessKeyHash));
        }

        AccessKeyId = accessKeyId;
        SecretAccessKeyHash = secretAccessKeyHash;
        UserName = userName;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = expiresAt;
        IsActive = true;
        Policies = new List<string>();
        Description = description;
    }

    /// <summary>
    /// Initializes a new instance of the AccessKey class for reconstruction from storage.
    /// </summary>
    /// <param name="accessKeyId">The access key ID.</param>
    /// <param name="secretAccessKeyHash">The secret key hash.</param>
    /// <param name="userName">The user name.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="expiresAt">The expiration date.</param>
    /// <param name="isActive">Whether the key is active.</param>
    /// <param name="policies">The policy references.</param>
    /// <param name="description">The description.</param>
    public AccessKey(
        string accessKeyId,
        string secretAccessKeyHash,
        string? userName,
        DateTime createdAt,
        DateTime? expiresAt,
        bool isActive,
        List<string> policies,
        string? description)
    {
        AccessKeyId = accessKeyId;
        SecretAccessKeyHash = secretAccessKeyHash;
        UserName = userName;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        IsActive = isActive;
        Policies = policies ?? new List<string>();
        Description = description;
    }

    /// <summary>
    /// Checks whether this access key is currently valid for authentication.
    /// </summary>
    /// <returns>True if the key is active and not expired; otherwise, false.</returns>
    public bool IsValid()
    {
        if (!IsActive)
        {
            return false;
        }

        if (ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Deactivates this access key.
    /// Once deactivated, the key cannot be used for authentication.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Activates this access key.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Adds a policy reference to this access key.
    /// </summary>
    /// <param name="policy">The policy reference to add.</param>
    public void AddPolicy(string policy)
    {
        if (string.IsNullOrWhiteSpace(policy))
        {
            throw new ArgumentException("Policy cannot be null or whitespace.", nameof(policy));
        }

        if (!Policies.Contains(policy))
        {
            Policies.Add(policy);
        }
    }

    /// <summary>
    /// Removes a policy reference from this access key.
    /// </summary>
    /// <param name="policy">The policy reference to remove.</param>
    /// <returns>True if the policy was removed; otherwise, false.</returns>
    public bool RemovePolicy(string policy)
    {
        return Policies.Remove(policy);
    }
}
