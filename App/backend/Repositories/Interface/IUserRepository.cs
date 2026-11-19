using DatSan.Api.Models;

namespace DatSan.Api.Repositories.Interface;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<User?> FindByIdAsync(long id, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);
    Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken);

    /// <summary>Inserts a user. Throws <see cref="DuplicateUserException"/> on a unique email/phone conflict.</summary>
    Task AddAsync(User user, CancellationToken cancellationToken);
}

/// <summary>Raised when the database unique index rejects an email or phone (e.g. two sign-ups at once).</summary>
public sealed class DuplicateUserException(string field, Exception? inner = null)
    : Exception($"Duplicate {field}", inner)
{
    public string Field { get; } = field;
}
