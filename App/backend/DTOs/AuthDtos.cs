using System.ComponentModel.DataAnnotations;
using System.Globalization;
using DatSan.Api.Models;

namespace DatSan.Api.DTOs;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(150, ErrorMessage = "Email không được quá 150 ký tự.")]
    public string Email { get; init; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(128, ErrorMessage = "Mật khẩu quá dài.")]
    public string Password { get; init; } = "";
}

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên từ 2 đến 100 ký tự.")]
    public string Name { get; init; } = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^\s*0\d{9}\s*$", ErrorMessage = "Số điện thoại gồm 10 chữ số và bắt đầu bằng 0.")]
    public string Phone { get; init; } = "";

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(150, ErrorMessage = "Email không được quá 150 ký tự.")]
    public string Email { get; init; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(72, MinimumLength = 6, ErrorMessage = "Mật khẩu từ 6 đến 72 ký tự.")]
    public string Password { get; init; } = "";

    // Admin accounts are never self-registered; the database role is player or owner only.
    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    [RegularExpression("^(player|owner)$", ErrorMessage = "Chỉ được đăng ký vai trò người chơi hoặc chủ sân.")]
    public string Role { get; init; } = "";
}

public sealed record UserDto(string Id, string Name, string Email, string Role)
{
    public static UserDto From(User user) => new(
        user.Id.ToString(CultureInfo.InvariantCulture), user.Name, user.Email, user.Role);
}

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);
public sealed record ApiResponse<T>(T Data);
public sealed record ApiError(string Message, string Code);
