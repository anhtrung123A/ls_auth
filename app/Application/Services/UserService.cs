using app.Application.Contracts;
using app.Constants;
using app.Infrastructure.Persistence;
using app.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace app.Application.Services;

public sealed class UserService(AuthDbContext db, ITokenService tokenService) : IUserService
{
    public async Task<ServiceResult> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var loginId = request.LoginId.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.LoginId == loginId, cancellationToken))
        {
            return ServiceResult.Failure(ErrorCodes.UserDuplicateLoginId, StatusCodes.Status409Conflict);
        }

        var generatedPassword = tokenService.GenerateRandomPassword();
        var user = new User
        {
            LoginId = loginId,
            PasswordHash = tokenService.HashValue(generatedPassword),
            RegisteredAtUtc = request.RegisteredAtUtc,
            PasswordChangedAtUtc = null,
            RegisteredByEmail = request.RegisteredByEmail.Trim().ToLowerInvariant(),
            UpdatedByEmail = request.UpdatedByEmail.Trim().ToLowerInvariant()
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult.Success(new
        {
            user.Id,
            user.LoginId,
            user.RegisteredAtUtc,
            user.RegisteredByEmail,
            user.UpdatedByEmail,
            GeneratedPassword = generatedPassword
        }, "User created successfully.", StatusCodes.Status201Created);
    }
}
