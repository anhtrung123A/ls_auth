using System.ComponentModel.DataAnnotations;

namespace app.Entities;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(255)]
    public required string LoginId { get; set; }

    [MaxLength(255)]
    public required string PasswordHash { get; set; }

    public DateTime RegisteredAtUtc { get; set; }

    public DateTime? PasswordChangedAtUtc { get; set; }

    [MaxLength(255)]
    public required string RegisteredByEmail { get; set; }

    [MaxLength(255)]
    public required string UpdatedByEmail { get; set; }
}
