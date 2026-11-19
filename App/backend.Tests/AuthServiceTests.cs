using System.IdentityModel.Tokens.Jwt;
using System.Text;
using DatSan.Api.Config;
using DatSan.Api.DTOs;
using DatSan.Api.Middleware;
using DatSan.Api.Models;
using DatSan.Api.Repositories.Interface;
using DatSan.Api.Services.Implements;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace DatSan.Api.Tests;

public sealed class AuthServiceTests
{
    private static readonly string PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct-password", 4);
    private static readonly JwtOptions Jwt = new()
    {
        Issuer = "test-api", Audience = "test-app",
        Key = "only-for-unit-tests-this-is-not-a-deployment-key-123456", LifetimeMinutes = 60
    };

    [Theory]
    [InlineData("player")]
    [InlineData("owner")]
    [InlineData("admin")]
    public async Task Login_issues_signed_token_with_database_role(string role)
    {
        var user = CreateUser(role);
        var result = await CreateService(user).LoginAsync(new LoginRequest
        {
            Email = "user@example.com", Password = "correct-password"
        }, CancellationToken.None);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(result.AccessToken, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = Jwt.Issuer,
            ValidateAudience = true, ValidAudience = Jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Jwt.Key)),
            ValidateLifetime = true, RoleClaimType = "role"
        }, out _);
        Assert.True(principal.IsInRole(role));
        Assert.Equal("9007199254740993", result.User.Id); // ID must not lose JS precision.
        Assert.Equal(user.Name, result.User.Name);
        Assert.InRange(result.ExpiresAt - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(59), TimeSpan.FromMinutes(61));
        Assert.DoesNotContain(handler.ReadJwtToken(result.AccessToken).Claims,
            claim => claim.Type.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unknown_user_and_wrong_password_have_same_public_error(bool userExists)
    {
        var error = await Assert.ThrowsAsync<ApiException>(() =>
            CreateService(userExists ? CreateUser() : null).LoginAsync(new LoginRequest
            {
                Email = "user@example.com", Password = "wrong-password"
            }, CancellationToken.None));
        Assert.Equal(401, error.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", error.Code);
    }

    [Fact]
    public async Task Locked_user_cannot_login_even_with_correct_password()
    {
        var user = CreateUser();
        user.Status = "locked";
        var error = await Assert.ThrowsAsync<ApiException>(() => CreateService(user).LoginAsync(
            new LoginRequest { Email = user.Email, Password = "correct-password" }, CancellationToken.None));
        Assert.Equal(403, error.StatusCode);
        Assert.Equal("ACCOUNT_LOCKED", error.Code);
    }

    [Fact]
    public async Task Invalid_database_role_cannot_login()
    {
        var error = await Assert.ThrowsAsync<ApiException>(() => CreateService(CreateUser("superuser"))
            .LoginAsync(new LoginRequest { Email = "user@example.com", Password = "correct-password" }, CancellationToken.None));
        Assert.Equal("INVALID_ROLE", error.Code);
    }

    [Fact]
    public async Task Malformed_stored_hash_does_not_grant_access()
    {
        var user = CreateUser();
        user.PasswordHash = "not-a-bcrypt-hash";
        var error = await Assert.ThrowsAsync<ApiException>(() => CreateService(user).LoginAsync(
            new LoginRequest { Email = user.Email, Password = "correct-password" }, CancellationToken.None));
        Assert.Equal("INVALID_CREDENTIALS", error.Code);
    }

    [Fact]
    public async Task Password_over_bcrypt_byte_limit_is_not_silently_truncated()
    {
        var password = new string('a', 72);
        var user = CreateUser();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 4);
        var error = await Assert.ThrowsAsync<ApiException>(() => CreateService(user).LoginAsync(
            new LoginRequest { Email = user.Email, Password = password + "x" }, CancellationToken.None));
        Assert.Equal("INVALID_CREDENTIALS", error.Code);
    }

    [Fact]
    public async Task Leading_and_trailing_password_spaces_are_preserved()
    {
        var user = CreateUser();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(" correct-password ", 4);
        var result = await CreateService(user).LoginAsync(new LoginRequest
        {
            Email = user.Email, Password = " correct-password "
        }, CancellationToken.None);
        Assert.Equal(user.Email, result.User.Email);
    }

    [Theory]
    [InlineData("player")]
    [InlineData("owner")]
    public async Task Register_saves_hashed_password_and_signs_in(string role)
    {
        var repository = new FakeUserRepository(null);
        var result = await CreateService(repository).RegisterAsync(NewAccount(role), CancellationToken.None);
        var saved = Assert.Single(repository.Added);
        Assert.Equal("new@example.com", saved.Email); // Trimmed and lower-cased.
        Assert.Equal("Nguyễn Văn A", saved.Name);      // Extra spaces collapsed.
        Assert.Equal("0911222333", saved.Phone);
        Assert.Equal(role, saved.Role);
        Assert.Equal("active", saved.Status);
        Assert.NotEqual("secret123", saved.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("secret123", saved.PasswordHash));
        Assert.Equal(role, result.User.Role);
        Assert.Equal("42", result.User.Id);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Admin")]
    [InlineData("superuser")]
    [InlineData("")]
    public async Task Register_cannot_create_admin_or_unknown_role(string role)
    {
        var repository = new FakeUserRepository(null);
        var error = await Assert.ThrowsAsync<ApiException>(() =>
            CreateService(repository).RegisterAsync(NewAccount(role), CancellationToken.None));
        Assert.Equal(400, error.StatusCode);
        Assert.Equal("INVALID_ROLE", error.Code);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task Register_rejects_existing_email_regardless_of_case()
    {
        var repository = new FakeUserRepository(CreateUser());
        var error = await Assert.ThrowsAsync<ApiException>(() => CreateService(repository)
            .RegisterAsync(NewAccount(email: "  USER@Example.com "), CancellationToken.None));
        Assert.Equal(409, error.StatusCode);
        Assert.Equal("EMAIL_TAKEN", error.Code);
        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task Register_rejects_existing_phone()
    {
        var repository = new FakeUserRepository(CreateUser());
        var error = await Assert.ThrowsAsync<ApiException>(() => CreateService(repository)
            .RegisterAsync(NewAccount(phone: "0900000009"), CancellationToken.None));
        Assert.Equal(409, error.StatusCode);
        Assert.Equal("PHONE_TAKEN", error.Code);
    }

    [Theory]
    [InlineData("phone", "PHONE_TAKEN")]
    [InlineData("email", "EMAIL_TAKEN")]
    public async Task Register_maps_database_unique_conflict_from_concurrent_signup(string field, string code)
    {
        var repository = new FakeUserRepository(null) { DuplicateOnAdd = field };
        var error = await Assert.ThrowsAsync<ApiException>(() =>
            CreateService(repository).RegisterAsync(NewAccount(), CancellationToken.None));
        Assert.Equal(409, error.StatusCode);
        Assert.Equal(code, error.Code);
    }

    [Fact]
    public async Task Register_rejects_password_over_bcrypt_byte_limit()
    {
        var repository = new FakeUserRepository(null);
        var request = new RegisterRequest
        {
            Name = "Người chơi", Phone = "0911222333", Email = "new@example.com",
            Password = new string('đ', 40), Role = "player" // 40 characters but 80 UTF-8 bytes.
        };
        var error = await Assert.ThrowsAsync<ApiException>(() =>
            CreateService(repository).RegisterAsync(request, CancellationToken.None));
        Assert.Equal("PASSWORD_TOO_LONG", error.Code);
        Assert.Empty(repository.Added);
    }

    private static RegisterRequest NewAccount(
        string role = "player", string email = "  New@Example.com ", string phone = "0911222333") => new()
    {
        Name = "  Nguyễn   Văn A ", Phone = phone, Email = email, Password = "secret123", Role = role
    };

    private static User CreateUser(string role = "player") => new()
    {
        Id = 9007199254740993, Name = "Người chơi thử", Email = "user@example.com", Phone = "0900000009",
        PasswordHash = PasswordHash, Role = role, Status = "active"
    };

    private static AuthService CreateService(User? user) => CreateService(new FakeUserRepository(user));

    private static AuthService CreateService(FakeUserRepository repository) => new(
        repository, new JwtTokenService(Options.Create(Jwt), TimeProvider.System));

    private sealed class FakeUserRepository(User? user) : IUserRepository
    {
        public List<User> Added { get; } = [];
        public string? DuplicateOnAdd { get; init; }

        public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult(user);
        public Task<User?> FindByIdAsync(long id, CancellationToken cancellationToken) => Task.FromResult(user);
        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(user is not null && user.Email == email);
        public Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken) =>
            Task.FromResult(user is not null && user.Phone == phone);

        public Task AddAsync(User newUser, CancellationToken cancellationToken)
        {
            if (DuplicateOnAdd is not null) throw new DuplicateUserException(DuplicateOnAdd);
            newUser.Id = 42;
            Added.Add(newUser);
            return Task.CompletedTask;
        }
    }
}
