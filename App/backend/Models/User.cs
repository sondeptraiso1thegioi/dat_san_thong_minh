namespace DatSan.Api.Models;

// Maps the existing dbo.users table. This project does not create database tables.
public sealed class User
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Address { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Emails are stored and looked up in lower case, so lookups work whatever the DB collation is.
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
