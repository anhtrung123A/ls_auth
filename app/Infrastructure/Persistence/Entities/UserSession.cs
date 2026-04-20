using System.ComponentModel.DataAnnotations;

namespace app.Infrastructure.Persistence.Entities;

public class UserSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    [MaxLength(128)]
    public required string RefreshTokenHash { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public Guid? ReplacedBySessionId { get; set; }

    public User? User { get; set; }
}
