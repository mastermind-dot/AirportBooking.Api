namespace AirportBooking.Domain.Entities;

/// <summary>
/// One issued refresh token, stored as a hash.
///
/// The raw token only ever exists in the HttpOnly cookie on the client. If this
/// table leaks, the hashes cannot be replayed — the same reason passwords are
/// hashed. <see cref="ReplacedByTokenHash"/> forms a rotation chain: a request
/// carrying an already-revoked token means it was stolen, and the whole chain
/// should be revoked.
/// </summary>
public class RefreshToken
{
    private RefreshToken() { } // EF Core

    public RefreshToken(Guid userId, string tokenHash, DateTime expiresAtUtc, string? createdByIp)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedByIp = createdByIp;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }
    public ApplicationUser User { get; private set; } = null!;

    /// <summary>SHA-256 of the raw token. Never store the token itself.</summary>
    public string TokenHash { get; private set; } = null!;

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public string? CreatedByIp { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsActive => !IsRevoked && !IsExpired;

    /// <summary>Revokes this token, optionally recording the token that replaced it.</summary>
    public void Revoke(string? replacedByTokenHash = null)
    {
        if (IsRevoked) return;

        RevokedAtUtc = DateTime.UtcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
