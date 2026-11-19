using DatSan.Api.Data;
using DatSan.Api.Models;
using DatSan.Api.Repositories.Interface;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DatSan.Api.Repositories.Implements;

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    // Direct comparison lets SQL Server use the uq_users_email index (no UPPER() table scan).
    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = User.NormalizeEmail(email);
        return db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email == normalized, cancellationToken);
    }

    public Task<User?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = User.NormalizeEmail(email);
        return db.Users.AnyAsync(x => x.Email == normalized, cancellationToken);
    }

    public Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(x => x.Phone == phone, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 } sql)
        {
            db.Entry(user).State = EntityState.Detached;
            var field = sql.Message.Contains("uq_users_phone", StringComparison.OrdinalIgnoreCase) ? "phone" : "email";
            throw new DuplicateUserException(field, error);
        }
    }
}
