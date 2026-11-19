using DatSan.Api.DTOs;
using DatSan.Api.Models;

namespace DatSan.Api.Services.Interface;

public interface IJwtTokenService
{
    AuthResponse CreateSession(User user);
}
