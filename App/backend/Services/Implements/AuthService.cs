using System.Text;
using System.Text.RegularExpressions;
using DatSan.Api.DTOs;
using DatSan.Api.Middleware;
using DatSan.Api.Models;
using DatSan.Api.Repositories.Interface;
using DatSan.Api.Services.Interface;

namespace DatSan.Api.Services.Implements;

public sealed class AuthService(IUserRepository users, IJwtTokenService tokens) : IAuthService
{
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("invalid-account-placeholder", 10);

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        // Existing database uses standard bcrypt (72-byte password limit).
        if (Encoding.UTF8.GetByteCount(request.Password) > 72)
            throw InvalidCredentials();

        var user = await users.FindByEmailAsync(request.Email, cancellationToken);
        var validPassword = false;
        try
        {
            // Also verify a hash for unknown users, avoiding an immediate timing difference.
            validPassword = BCrypt.Net.BCrypt.Verify(request.Password, user?.PasswordHash ?? DummyHash);
        }
        catch (Exception error) when (error is BCrypt.Net.SaltParseException or ArgumentException)
        {
            // A malformed stored hash must never grant access or expose the hash in an API error.
        }

        if (user is null || !validPassword)
            throw InvalidCredentials();
        if (user.Status != "active")
            throw new ApiException(403, "ACCOUNT_LOCKED", "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.");
        if (user.Role is not ("player" or "owner" or "admin"))
            throw new ApiException(403, "INVALID_ROLE", "Tài khoản chưa có vai trò hợp lệ.");

        return tokens.CreateSession(user);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var name = Whitespace.Replace(request.Name.Trim(), " ");
        var phone = request.Phone.Trim();
        var email = User.NormalizeEmail(request.Email);
        if (name.Length is < 2 or > 100)
            throw new ApiException(400, "VALIDATION_ERROR", "Họ tên từ 2 đến 100 ký tự.");
        // Checked again here (not only by the DTO) so no code path can create an admin.
        if (request.Role is not ("player" or "owner"))
            throw new ApiException(400, "INVALID_ROLE", "Chỉ được đăng ký vai trò người chơi hoặc chủ sân.");
        if (Encoding.UTF8.GetByteCount(request.Password) > 72)
            throw new ApiException(400, "PASSWORD_TOO_LONG", "Mật khẩu quá dài. Vui lòng dùng mật khẩu ngắn hơn.");
        if (await users.EmailExistsAsync(email, cancellationToken))
            throw EmailTaken();
        if (await users.PhoneExistsAsync(phone, cancellationToken))
            throw PhoneTaken();

        var user = new User
        {
            Name = name, Email = email, Phone = phone, Role = request.Role, Status = "active",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 10) // Same cost as demo data.
        };
        try
        {
            await users.AddAsync(user, cancellationToken);
        }
        catch (DuplicateUserException error)
        {
            // Another sign-up won the race between the check above and the insert.
            throw error.Field == "phone" ? PhoneTaken() : EmailTaken();
        }
        return tokens.CreateSession(user);
    }

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private static ApiException EmailTaken() =>
        new(409, "EMAIL_TAKEN", "Email này đã được đăng ký. Vui lòng đăng nhập hoặc dùng email khác.");

    private static ApiException PhoneTaken() =>
        new(409, "PHONE_TAKEN", "Số điện thoại này đã được đăng ký.");

    private static ApiException InvalidCredentials() =>
        new(401, "INVALID_CREDENTIALS", "Email hoặc mật khẩu không chính xác.");
}
