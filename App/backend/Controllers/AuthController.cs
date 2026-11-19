using DatSan.Api.DTOs;
using DatSan.Api.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DatSan.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(
        LoginRequest request, CancellationToken cancellationToken) =>
        Ok(new ApiResponse<AuthResponse>(await auth.LoginAsync(request, cancellationToken)));

    // Self-registration for player/owner; returns a session so the app signs in right away.
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register(
        RegisterRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created,
            new ApiResponse<AuthResponse>(await auth.RegisterAsync(request, cancellationToken)));
}
